// Copyright 2020 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Xml;
using System.Xml.XPath;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    // Processes Natvis xml and logs WARNINGS for unsupported tags and attributes.
    public class NatvisValidator
    {
        public class Factory
        {
            IFileSystem fileSystem;
            NatvisDiagnosticLogger logger;

            public Factory(IFileSystem fileSystem, NatvisDiagnosticLogger logger)
            {
                this.fileSystem = fileSystem;
                this.logger = logger;
            }

            public NatvisValidator Create()
            {
                return new NatvisValidator(fileSystem, logger);
            }
        }

        // Groups the XPath expression with a warning message.
        struct Validator
        {
            public Validator(XPathExpression xpathExpression, string warningMessage)
            {
                XPathExpression = xpathExpression;
                WarningMessage = warningMessage;
            }

            public XPathExpression XPathExpression { get; private set; }

            public string WarningMessage { get; private set; }
        }

        // Builds Validator objects and properly binds in the XmlNamespaceManager.
        class ValidatorBuilder
        {
            XmlNamespaceManager namespaceManager;

            public ValidatorBuilder(XmlNamespaceManager namespaceManager)
            {
                this.namespaceManager = namespaceManager;
            }

            public Validator Build(string xpathExpression, string warningMessage)
            {
                var compiledExpression = XPathExpression.Compile(xpathExpression);
                compiledExpression.SetContext(namespaceManager);
                return new Validator(compiledExpression, warningMessage);
            }
        }

        IFileSystem fileSystem;
        NatvisDiagnosticLogger logger;

        private NatvisValidator(IFileSystem fileSystem, NatvisDiagnosticLogger logger)
        {
            this.fileSystem = fileSystem;
            this.logger = logger;
        }

        public void Validate(string filepath)
        {
            using (Stream stream = fileSystem.FileStream.Create(filepath, FileMode.Open,
                                                                FileAccess.Read, FileShare.Read))
            {
                Validate(stream);
            }
        }

        public void Validate(Stream stream)
        {
            var doc = new XPathDocument(stream);
            var navigator = doc.CreateNavigator();
            var namespaceUri = @"http://schemas.microsoft.com/vstudio/debugger/natvis/2010";
            var namespaceManager = new XmlNamespaceManager(navigator.NameTable);
            namespaceManager.AddNamespace("vs", namespaceUri);
            var validatorBuilder = new ValidatorBuilder(namespaceManager);

            List<Validator> validators = new List<Validator>();

            validators.Add(validatorBuilder.Build(@"count(//vs:Synthetic[@Expression])",
                "The 'Expression' attribute is not supported on the <Synthetic> tag."));
            validators.Add(validatorBuilder.Build(@"count(//@*[name()='ModuleName'])",
                "The 'ModuleName' attribute is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//@*[name()='ModuleVersionMin'])",
                "The 'ModuleVersionMin' attribute is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//@*[name()='ModuleVersionMax'])",
                "The 'ModuleVersionMax' attribute is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:DisplayString[@Export])",
                "The 'Export' attribute is not supported on the <DisplayString> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:DisplayString[@Encoding])",
                "The 'Encoding' attribute is not supported on the <DisplayString> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:DisplayString[@LegacyAddin])",
                "The 'LegacyAddin' attribute is not supported on the <DisplayString> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:UIVisualizer)",
                "The <UIVisualizer> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:CustomVisualizer)",
                "The <CustomVisualizer> tag is not supported."));
            validators.Add(
                validatorBuilder.Build(@"count(//vs:LinkedListItems/vs:ValueNode[@Name])",
                "The 'Name' attribute is not supported on the <LinkedListItems>/<ValueNode> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:ArrayItems/vs:Direction)",
                "The <ArrayItems>/<Direction> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:ArrayItems/vs:Rank)",
                "The <ArrayItems>/<Rank> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:ArrayItems/vs:LowerBound)",
                "The <ArrayItems>/<LowerBound> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Type/vs:MostDerivedType)",
                "The <Type>/<MostDerivedType> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Type/vs:Version)",
                "The <Type>/<Version> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Type[@Priority])",
                "The 'Priority' attribute is not supported on the <Type> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Type[@Inheritable])",
                "The 'Inheritable' attribute is not supported on the <Type> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:AlternativeType[@Priority])",
                "The 'Priority' attribute is not supported on the <AlternativeType> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:AlternativeType[@Inheritable])",
                "The 'Inheritable' attribute is not supported on the <AlternativeType> tag."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Intrinsic)",
                "The <Intrinsic> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:HResult)",
                "The <HResult> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:LocalizedStrings)",
                "The <LocalizedStrings> tag is not supported."));
            validators.Add(validatorBuilder.Build(@"count(//vs:Version)",
                "The <Version> tag is not supported."));

            foreach (var validator in validators)
            {
                var xPathResult = navigator.Evaluate(validator.XPathExpression);
                if (xPathResult is double)
                {
                    if ((double)xPathResult > 0)
                    {
                        logger.Warning(() => $"{validator.WarningMessage} It was found " +
                            $"{xPathResult} time(s).");
                    }
                }
                else
                {
                    Trace.WriteLine($"ERROR: Unexpected XPath result type=" +
                        $"{xPathResult?.GetType()} for expression={validator.XPathExpression}.");
                }
            }
        }
    }
}