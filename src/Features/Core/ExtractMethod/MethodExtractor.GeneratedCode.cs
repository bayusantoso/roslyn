// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.ExtractMethod
{
    internal abstract partial class MethodExtractor
    {
        internal class GeneratedCode
        {
            public GeneratedCode(
                OperationStatus status,
                SemanticDocument document,
                SyntaxAnnotation methodNameAnnotation,
                SyntaxAnnotation callsiteAnnotation,
                SyntaxAnnotation methodDefinitionAnnotation)
            {
                Contract.ThrowIfNull(document);
                Contract.ThrowIfNull(methodNameAnnotation);
                Contract.ThrowIfNull(callsiteAnnotation);
                Contract.ThrowIfNull(methodDefinitionAnnotation);

                this.Status = status;
                this.SemanticDocument = document;
                this.MethodNameAnnotation = methodNameAnnotation;
                this.CallSiteAnnotation = callsiteAnnotation;
                this.MethodDefinitionAnnotation = methodDefinitionAnnotation;
            }

            public OperationStatus Status { get; private set; }
            public SemanticDocument SemanticDocument { get; private set; }

            public SyntaxAnnotation MethodNameAnnotation { get; private set; }
            public SyntaxAnnotation CallSiteAnnotation { get; private set; }
            public SyntaxAnnotation MethodDefinitionAnnotation { get; private set; }
        }
    }
}
