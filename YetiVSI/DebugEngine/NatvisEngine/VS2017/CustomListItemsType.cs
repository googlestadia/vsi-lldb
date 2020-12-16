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

﻿namespace YetiVSI.DebugEngine.NatvisEngine
{
    // These classes are hand-coded because the xsd code generation tool can't handle the recursive
    // structure.
    public partial class CustomListItemsType
    {
        [System.Xml.Serialization.XmlElementAttribute("Variable")]
        public VariableType[] Variable { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool Optional { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool OptionalSpecified { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("IncludeView")]
        public string IncludeView { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("ExcludeView")]
        public string ExcludeView { get; set; }

        [System.Xml.Serialization.XmlElementAttribute("Loop", typeof(LoopType))]
        [System.Xml.Serialization.XmlElementAttribute("If", typeof(IfType))]
        [System.Xml.Serialization.XmlElementAttribute("Elseif", typeof(ElseifType))]
        [System.Xml.Serialization.XmlElementAttribute("Else", typeof(ElseType))]
        [System.Xml.Serialization.XmlElementAttribute("Exec", typeof(ExecType))]
        [System.Xml.Serialization.XmlElementAttribute("Break", typeof(BreakType))]
        [System.Xml.Serialization.XmlElementAttribute("Item", typeof(CustomListItemType))]

        public object[] CodeBlock { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class VariableType
    {
        [System.Xml.Serialization.XmlAttributeAttribute("Name")]
        public string Name { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("InitialValue")]
        public string InitialValue { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class LoopType
    {
        [System.Xml.Serialization.XmlElementAttribute("Loop", typeof(LoopType))]
        [System.Xml.Serialization.XmlElementAttribute("If", typeof(IfType))]
        [System.Xml.Serialization.XmlElementAttribute("Elseif", typeof(ElseifType))]
        [System.Xml.Serialization.XmlElementAttribute("Else", typeof(ElseType))]
        [System.Xml.Serialization.XmlElementAttribute("Exec", typeof(ExecType))]
        [System.Xml.Serialization.XmlElementAttribute("Break", typeof(BreakType))]
        [System.Xml.Serialization.XmlElementAttribute("Item", typeof(CustomListItemType))]
        public object[] CodeBlock { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class BreakType
    {
        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class ExecType
    {
        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class IfType
    {
        [System.Xml.Serialization.XmlElementAttribute("Loop", typeof(LoopType))]
        [System.Xml.Serialization.XmlElementAttribute("If", typeof(IfType))]
        [System.Xml.Serialization.XmlElementAttribute("Elseif", typeof(ElseifType))]
        [System.Xml.Serialization.XmlElementAttribute("Else", typeof(ElseType))]
        [System.Xml.Serialization.XmlElementAttribute("Exec", typeof(ExecType))]
        [System.Xml.Serialization.XmlElementAttribute("Break", typeof(BreakType))]
        [System.Xml.Serialization.XmlElementAttribute("Item", typeof(CustomListItemType))]
        public object[] CodeBlock { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class ElseifType
    {
        [System.Xml.Serialization.XmlElementAttribute("Loop", typeof(LoopType))]
        [System.Xml.Serialization.XmlElementAttribute("If", typeof(IfType))]
        [System.Xml.Serialization.XmlElementAttribute("Elseif", typeof(ElseifType))]
        [System.Xml.Serialization.XmlElementAttribute("Else", typeof(ElseType))]
        [System.Xml.Serialization.XmlElementAttribute("Exec", typeof(ExecType))]
        [System.Xml.Serialization.XmlElementAttribute("Break", typeof(BreakType))]
        [System.Xml.Serialization.XmlElementAttribute("Item", typeof(CustomListItemType))]
        public object[] CodeBlock { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class ElseType
    {
        [System.Xml.Serialization.XmlElementAttribute("Loop", typeof(LoopType))]
        [System.Xml.Serialization.XmlElementAttribute("If", typeof(IfType))]
        [System.Xml.Serialization.XmlElementAttribute("Elseif", typeof(ElseifType))]
        [System.Xml.Serialization.XmlElementAttribute("Else", typeof(ElseType))]
        [System.Xml.Serialization.XmlElementAttribute("Exec", typeof(ExecType))]
        [System.Xml.Serialization.XmlElementAttribute("Break", typeof(BreakType))]
        [System.Xml.Serialization.XmlElementAttribute("Item", typeof(CustomListItemType))]
        public object[] CodeBlock { get; set; }
    }

    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(
        Namespace = "http://schemas.microsoft.com/vstudio/debugger/natvis/2010")]
    public class CustomListItemType
    {
        [System.Xml.Serialization.XmlAttributeAttribute("Condition")]
        public string Condition { get; set; }

        [System.Xml.Serialization.XmlAttributeAttribute("Name")]
        public string Name { get; set; }

        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value { get; set; }
    }
}
