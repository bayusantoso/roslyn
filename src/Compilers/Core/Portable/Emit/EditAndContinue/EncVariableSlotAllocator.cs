﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Emit
{
    internal sealed class EncVariableSlotAllocator : VariableSlotAllocator
    {
        // symbols:
        private readonly SymbolMatcher _symbolMap;

        // syntax:
        private readonly Func<SyntaxNode, SyntaxNode> _syntaxMapOpt;
        private readonly IMethodSymbolInternal _previousMethod;

        // locals:
        private readonly IReadOnlyDictionary<EncLocalInfo, int> _previousLocalSlots;
        private readonly ImmutableArray<EncLocalInfo> _previousLocals;

        // previous state machine:
        private readonly string _stateMachineTypeNameOpt;
        private readonly int _hoistedLocalSlotCount;
        private readonly IReadOnlyDictionary<EncHoistedLocalInfo, int> _hoistedLocalSlotsOpt;
        private readonly int _awaiterCount;
        private readonly IReadOnlyDictionary<Cci.ITypeReference, int> _awaiterMapOpt;

        public EncVariableSlotAllocator(
            SymbolMatcher symbolMap,
            Func<SyntaxNode, SyntaxNode> syntaxMapOpt,
            IMethodSymbolInternal previousMethod,
            ImmutableArray<EncLocalInfo> previousLocals,
            string stateMachineTypeNameOpt,
            int hoistedLocalSlotCount,
            IReadOnlyDictionary<EncHoistedLocalInfo, int> hoistedLocalSlotsOpt,
            int awaiterCount,
            IReadOnlyDictionary<Cci.ITypeReference, int> awaiterMapOpt)
        {
            Debug.Assert(symbolMap != null);
            Debug.Assert(previousMethod != null);
            Debug.Assert(!previousLocals.IsDefault);

            _symbolMap = symbolMap;
            _syntaxMapOpt = syntaxMapOpt;
            _previousLocals = previousLocals;
            _previousMethod = previousMethod;
            _hoistedLocalSlotsOpt = hoistedLocalSlotsOpt;
            _hoistedLocalSlotCount = hoistedLocalSlotCount;
            _stateMachineTypeNameOpt = stateMachineTypeNameOpt;
            _awaiterCount = awaiterCount;
            _awaiterMapOpt = awaiterMapOpt;

            // Create a map from local info to slot.
            var previousLocalInfoToSlot = new Dictionary<EncLocalInfo, int>();
            for (int slot = 0; slot < previousLocals.Length; slot++)
            {
                var localInfo = previousLocals[slot];
                Debug.Assert(!localInfo.IsDefault);
                if (localInfo.IsUnused)
                {
                    // Unrecognized or deleted local.
                    continue;
                }

                previousLocalInfoToSlot.Add(localInfo, slot);
            }

            _previousLocalSlots = previousLocalInfoToSlot;
        }

        public override void AddPreviousLocals(ArrayBuilder<Cci.ILocalDefinition> builder)
        {
            builder.AddRange(_previousLocals.Select((info, index) => new SignatureOnlyLocalDefinition(info.Signature, index)));
        }

        private bool TryGetPreviousLocalId(SyntaxNode currentDeclarator, LocalDebugId currentId, out LocalDebugId previousId)
        {
            if (_syntaxMapOpt == null)
            {
                // no syntax map 
                // => the source of the current method is the same as the source of the previous method 
                // => relative positions are the same 
                // => synthesized ids are the same
                previousId = currentId;
                return true;
            }

            SyntaxNode previousDeclarator = _syntaxMapOpt(currentDeclarator);
            if (previousDeclarator == null)
            {
                previousId = default(LocalDebugId);
                return false;
            }

            int syntaxOffset = _previousMethod.CalculateLocalSyntaxOffset(previousDeclarator.SpanStart, previousDeclarator.SyntaxTree);
            previousId = new LocalDebugId(syntaxOffset, currentId.Ordinal);
            return true;
        }

        public override LocalDefinition GetPreviousLocal(
            Cci.ITypeReference currentType,
            ILocalSymbolInternal currentLocalSymbol,
            string nameOpt,
            SynthesizedLocalKind kind,
            LocalDebugId id,
            uint pdbAttributes,
            LocalSlotConstraints constraints,
            bool isDynamic,
            ImmutableArray<TypedConstant> dynamicTransformFlags)
        {
            if (id.IsNone)
            {
                return null;
            }

            LocalDebugId previousId;
            if (!TryGetPreviousLocalId(currentLocalSymbol.GetDeclaratorSyntax(), id, out previousId))
            {
                return null;
            }

            var previousType = _symbolMap.MapReference(currentType);
            if (previousType == null)
            {
                return null;
            }

            // TODO (bug #781309): Should report a warning if the type of the local has changed
            // and the previous value will be dropped.
            var localKey = new EncLocalInfo(new LocalSlotDebugInfo(kind, previousId), previousType, constraints, signature: null);

            int slot;
            if (!_previousLocalSlots.TryGetValue(localKey, out slot))
            {
                return null;
            }

            return new LocalDefinition(
                currentLocalSymbol,
                nameOpt,
                currentType,
                slot,
                kind,
                id,
                pdbAttributes,
                constraints,
                isDynamic,
                dynamicTransformFlags);
        }

        public override string PreviousStateMachineTypeName
        {
            get { return _stateMachineTypeNameOpt; }
        }

        public override int GetPreviousHoistedLocalSlotIndex(SyntaxNode currentDeclarator, Cci.ITypeReference currentType, SynthesizedLocalKind synthesizedKind, LocalDebugId currentId)
        {
            Debug.Assert(_hoistedLocalSlotsOpt != null);

            LocalDebugId previousId;
            if (!TryGetPreviousLocalId(currentDeclarator, currentId, out previousId))
            {
                return -1;
            }

            var previousType = _symbolMap.MapReference(currentType);
            if (previousType == null)
            {
                return -1;
            }

            // TODO (bug #781309): Should report a warning if the type of the local has changed
            // and the previous value will be dropped.
            var localKey = new EncHoistedLocalInfo(new LocalSlotDebugInfo(synthesizedKind, previousId), previousType);

            int slotIndex;
            if (!_hoistedLocalSlotsOpt.TryGetValue(localKey, out slotIndex))
            {
                return -1;
            }

            return slotIndex;
        }

        public override int PreviousHoistedLocalSlotCount
        {
            get { return _hoistedLocalSlotCount; }
        }

        public override int GetPreviousAwaiterSlotIndex(Cci.ITypeReference currentType)
        {
            Debug.Assert(_awaiterMapOpt != null);

            int slotIndex;
            return _awaiterMapOpt.TryGetValue(_symbolMap.MapReference(currentType), out slotIndex) ? slotIndex : -1;
        }

        public override int PreviousAwaiterSlotCount
        {
            get { return _awaiterCount; }
        }
    }
}
