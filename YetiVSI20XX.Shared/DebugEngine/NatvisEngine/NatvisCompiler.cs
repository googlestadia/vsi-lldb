using DebuggerApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YetiVSI.DebugEngine.Variables;

namespace YetiVSI.DebugEngine.NatvisEngine
{
    class NatvisCompiler
    {
        readonly RemoteTarget _target;
        readonly SbType _scope;
        readonly NatvisDiagnosticLogger _logger;
        readonly VsExpressionCreator _vsExpressionCreator;

        bool IsPointerType(SbType type)
        {
            return type != null && type.GetTypeFlags().HasFlag(TypeFlags.IS_POINTER);
        }

        public NatvisCompiler(RemoteTarget target, SbType scope, NatvisDiagnosticLogger logger)
        {
            _target = target;
            _scope = scope;
            _logger = logger;
            _vsExpressionCreator = new VsExpressionCreator();
        }

        class Context
        {
            public string VisualizerName { get; }
            public IDictionary<string, SbType> Arguments { get; private set; }
            public IDictionary<string, string> ScopedNames { get; }
            public SbType Scope { get; set; }

            public Context(string name, IDictionary<string, string> scopedNames, SbType scope)
            {
                VisualizerName = name;
                Arguments = new Dictionary<string, SbType>();
                ScopedNames = new Dictionary<string, string>(scopedNames);
                Scope = scope;
            }

            public Context Clone()
            {
                Context context = new Context(VisualizerName, ScopedNames, Scope);
                context.Arguments = new Dictionary<string, SbType>(Arguments);
                return context;
            }
        }

        bool IsIntegerType(SbType type)
        {
            return type != null && type.GetTypeFlags().HasFlag(TypeFlags.IS_INTEGER);
        }

        /// <summary>
        /// Handles <Type> element.
        ///
        /// Possible child elements:
        ///  <AlternativeType>
        ///  <Version>
        ///  <DisplayString>
        ///  <StringView>
        ///  <Expand>
        ///  <SmartPointer>       (not supported)
        ///  <Intrinsic>          (not supported)
        ///  <MostDerivedObject>  (not supported)
        ///  <CustomVisualizer>   (not supported)
        ///  <UIVisualizer>       (not supported)
        ///
        /// Possible attributes:
        ///  Name
        ///  Priority
        ///  Inheritable
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        public async Task<bool> IsCompilableAsync(VisualizerInfo vizInfo)
        {
            if (vizInfo.Visualizer.Items == null)
            {
                // The array is null if the visualizer is empty. This is valid and the expected
                // behaviour is to list fields of the type.
                return true;
            }

            var context =
                new Context(vizInfo.Visualizer.Name, vizInfo.NatvisScope.ScopedNames, _scope);

            foreach (object obj in vizInfo.Visualizer.Items)
            {
                if (obj is DisplayStringType displayString)
                {
                    if (!await HandleDisplayStringAsync(displayString, context))
                    {
                        return false;
                    }
                }
                else if (obj is StringViewType stringView)
                {
                    if (!await HandleStringViewAsync(stringView, context))
                    {
                        return false;
                    }
                }
                else if (obj is ExpandType expand)
                {
                    if (!await HandleExpandAsync(expand, context))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Handles <DisplayString> element.
        ///
        /// Child element:
        ///  formattable string expression
        ///
        /// Possible attributes:
        ///  LegacyAddin
        ///  Export
        ///  Encoding
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleDisplayStringAsync(DisplayStringType displayString, Context context)
        {
            if (displayString.Optional)
            {
                return true;
            }

            return await HandleFormattableStringAsync(displayString.Value, context) &&
                   await HandleExpressionAsync(displayString.Condition, context);
        }

        async Task<bool> HandleDisplayStringGroupAsync(DisplayStringType[] displayStringGroup,
                                                       Context context)
        {
            if (displayStringGroup == null)
            {
                // The array is null if there is no <DisplayString> in the parent element.
                return true;
            }

            foreach (var displayString in displayStringGroup)
            {
                if (!await HandleDisplayStringAsync(displayString, context))
                {
                    return false;
                }
                if (!displayString.Optional && displayString.Condition == null &&
                    displayString.ExcludeView == null && displayString.IncludeView == null)
                {
                    // It's safe to stop after an element without Condition or Optional.
                    // All subsequent elements certainly won't be used.
                    return true;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles string expression that is used by DisplayString and several other elements.
        /// Example: "size={size} and pair={{{x}, {y}}}".
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        // TODO: Implement a separate helper class which can be reused by both
        // NatvisCompiler and NatvisStringFormatter.FormatValueAsync.
        async Task<bool> HandleFormattableStringAsync(string value, Context context)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            int n = value.Length;
            int startPos = -1;
            for (int i = 0; i < n; ++i)
            {
                // The native VS allows '}' for missing opening '{'.
                if (startPos != -1 && value[i] == '}')
                {
                    var expressionSubstring = value.Substring(startPos, i - startPos);
                    if (!await HandleExpressionAsync(expressionSubstring, context))
                    {
                        return false;
                    }
                    startPos = -1;
                }
                else if (value[i] == '{')
                {
                    if (i + 1 < n && value[i + 1] == '{')
                    {
                        i++;
                    }
                    else
                    {
                        startPos = i + 1;
                    }
                }
            }
            // VS doesn't allow '{' without enclosing '}', but we allow it for simplicity.
            return true;
        }

        /// <summary>
        /// Handles <StringView> element.
        ///
        /// Child element:
        ///  expression string
        ///
        /// Attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleStringViewAsync(StringViewType stringView, Context context)
        {
            if (stringView.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(stringView.Condition, context) &&
                   await HandleExpressionAsync(stringView.Value, context);
        }

        /// <summary>
        /// Handles a group of <StringView> elements.
        /// </summary>
        /// <returns>Boolean value indicating correctness of non-optional expressions.</returns>
        async Task<bool> HandleStringViewGroupAsync(StringViewType[] stringViewGroup,
                                                    Context context)
        {
            if (stringViewGroup == null)
            {
                // The array is null if there is no <StringView> in the parent element.
                return true;
            }

            foreach (var stringView in stringViewGroup)
            {
                if (!await HandleStringViewAsync(stringView, context))
                {
                    return false;
                }
                if (!stringView.Optional && stringView.Condition == null &&
                    stringView.ExcludeView == null && stringView.IncludeView == null)
                {
                    // It's safe to stop after an element without Condition or Optional.
                    // All subsequent elements certainly won't be used.
                    return true;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles <Expand> element.
        ///
        /// Possible child elements:
        ///  <Item>
        ///  <ArrayItems>
        ///  <IndexListItems>
        ///  <LinkedListItems>
        ///  <TreeItems>
        ///  <ExpandedItem>
        ///  <Synthetic>
        ///  <CustomListItems>
        ///
        /// Possible attributes:
        ///  HideRawView
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleExpandAsync(ExpandType expand, Context context)
        {
            if (expand == null || expand.Items == null)
            {
                // `expand` is null if not defined in <Synthetic>.
                // `expand.Items` is null if the array is empty.
                return true;
            }

            // Handle all children.
            foreach (object obj in expand.Items)
            {
                if (!await HandleExpandOptionAsync(obj, context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles a single unnamed element of <Expand>.
        /// </summary>
        async Task<bool> HandleExpandOptionAsync(object obj, Context context)
        {
            if (obj is ItemType item)
            {
                return await HandleItemAsync(item, context);
            }
            else if (obj is ArrayItemsType arrayItems)
            {
                return await HandleArrayItemsAsync(arrayItems, context);
            }
            else if (obj is IndexListItemsType indexListItems)
            {
                return await HandleIndexListItemsAsync(indexListItems, context);
            }
            else if (obj is LinkedListItemsType linkedListItems)
            {
                return await HandleLinkedListItemsAsync(linkedListItems, context);
            }
            else if (obj is TreeItemsType treeItems)
            {
                return await HandleTreeItemsAsync(treeItems, context);
            }
            else if (obj is ExpandedItemType expandedItem)
            {
                return await HandleExpandedItemAsync(expandedItem, context);
            }
            else if (obj is SyntheticItemType syntheticItem)
            {
                return await HandleSyntheticItemAsync(syntheticItem, context);
            }
            else if (obj is CustomListItemsType customListItems)
            {
                return await HandleCustomListItemsAsync(customListItems, context);
            }

            Debug.Fail("All <Expand> options are exhausted.");
            return false;
        }

        /// <summary>
        /// Handles <Item> element (direct child of <Expand>).
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Name
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleItemAsync(ItemType item, Context context)
        {
            if (item.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(item.Condition, context) &&
                   await HandleExpressionAsync(item.Value, context);
        }

        /// <summary>
        /// Handles <ArrayItems> element.
        ///
        /// Possible child elements:
        ///  <Size>
        ///  <ValuePointer>
        ///  <LowerBound>   (not supported)
        ///  <Direction>    (not supported)
        ///  <Rank>         (not supported)
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleArrayItemsAsync(ArrayItemsType arrayItems, Context context)
        {
            if (arrayItems.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(arrayItems.Condition, context) &&
                   await HandleSizeGroupAsync(arrayItems.Size, context) &&
                   await HandleValuePointerGroupAsync(arrayItems.ValuePointer, context);
        }

        /// <summary>
        /// Handles a group of <ValuePointer> element.
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleValuePointerGroupAsync(ValuePointerType[] valuePointerGroup,
                                                      Context context)
        {
            foreach (var valuePointer in valuePointerGroup)
            {
                if (!await HandleExpressionAsync(valuePointer.Condition, context) ||
                    !await HandleExpressionAsync(valuePointer.Value, context))
                {
                    return false;
                }
                // It's safe to stop after an element with no Condition specified.
                // All subsequent elements certainly won't be used.
                if (valuePointer.Condition == null)
                {
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// Handles a group of <Size> elements.
        /// </summary>
        /// <returns>Boolean value indicating correctness of non-optional expressions.</returns>
        async Task<bool> HandleSizeGroupAsync(SizeType[] sizeGroup, Context context)
        {
            if (sizeGroup == null)
            {
                // The array is null if there are no <Size> elements.
                return true;
            }

            foreach (var size in sizeGroup)
            {
                if (!await HandleSizeAsync(size, context))
                {
                    return false;
                }
                // It's safe to stop after an element which isn't optional or conditioned.
                // All subsequent elements certainly won't be used.
                if (!size.Optional && size.Condition == null && size.ExcludeView == null &&
                    size.IncludeView == null)
                {
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// Handles a group of <ValueNode> elements (direct children of <IndexListItems>).
        /// </summary>
        /// <returns>Boolean value indicating correctness of expressions.</returns>
        async Task<bool> HandleValueNodeGroupAsync(IndexNodeType[] valueNodeGroup, Context context)
        {
            if (valueNodeGroup == null)
            {
                // The array is null if there is no <ValueNode> defined. This is not valid.
                _logger.Error("(Natvis) Content of element <IndexListItems> is incomplete: " +
                              "no <ValueNode> found.");
                return false;
            }

            foreach (var valueNode in valueNodeGroup)
            {
                if (!await HandleValueNodeAsync(valueNode, context))
                {
                    return false;
                }
                // It's safe to stop after an element with no Condition specified.
                // All subsequent elements certainly won't be used.
                if (valueNode.Condition == null)
                {
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// Handles <IndexListItems> element.
        ///
        /// Possible child elements:
        ///  <Size>
        ///  <ValueNode>
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleIndexListItemsAsync(IndexListItemsType indexListItems,
                                                   Context context)
        {
            if (indexListItems.Optional)
            {
                return true;
            }

            // IndexListItems introduce a new scope name "$i" of type "unsigned int".
            // Right now it is sufficient to clone the context and set "$i = 0U".
            // TODO: Before reusing parsed Natvis for future evaluations, we should
            // use context argument ("$i" => "unsigned int") instead.
            context = context.Clone();
            context.ScopedNames.Add("$i", "0U");

            return await HandleExpressionAsync(indexListItems.Condition, context) &&
                   await HandleSizeGroupAsync(indexListItems.Size, context) &&
                   await HandleValueNodeGroupAsync(indexListItems.ValueNode, context);
        }

        /// <summary>
        /// Handles <LinkedListItems> element.
        ///
        /// Possible child elements:
        ///  <Size>
        ///  <HeadPointer>
        ///  <NextPointer>
        ///  <ValueNode>
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleLinkedListItemsAsync(LinkedListItemsType linkedListItems,
                                                    Context context)
        {
            if (linkedListItems.Optional)
            {
                return true;
            }

            if (!await HandleSizeGroupAsync(linkedListItems.Size, context) ||
                !await HandleExpressionAsync(linkedListItems.Condition, context))
            {
                return false;
            }

            // The node type should be pointer.
            var headType = await CompileExpressionAsync(linkedListItems.HeadPointer, context);
            if (!IsPointerType(headType))
            {
                _logger.Error($"(Natvis) Invalid <HeadPointer>: '{linkedListItems.HeadPointer}' " +
                              "isn't a pointer type.");
                return false;
            }

            // The rest expressions are evaluated in the context of the node type.
            context = context.Clone();
            context.Scope = headType.GetPointeeType();

            // The next pointer type should be equal to `nodeType`.
            var nextType = await CompileExpressionAsync(linkedListItems.NextPointer, context);
            if (!IsPointerType(nextType))
            {
                _logger.Error($"(Natvis) Invalid <NextPointer>: '{linkedListItems.NextPointer}' " +
                              "isn't a pointer type.");
                return false;
            }
            if (nextType.GetName() != headType.GetName())
            {
                // TODO: There could be different types of the same name, but name
                // comparison should be OK for now.
                // The native VS is strict about type equality (e.g. the next pointer type isn't
                // allowed to be dervied from the head pointer type).
                _logger.Error("(Natvis) <HeadPointer> and <NextPointer> don't evaluate to the " +
                              $"same type. Expected '{headType.GetName()}', got " +
                              $"'{nextType.GetName()}'.");
                return false;
            }

            return await HandleListItemsNodeAsync(linkedListItems.ValueNode, context);
        }

        /// <summary>
        /// Handles <ValueNode> element (direct child of <LinkedListItems>).
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Name (formattable)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleListItemsNodeAsync(ListItemsNodeType listItemsNode, Context context)
        {
            if (listItemsNode == null)
            {
                // The value is null if there's no <ValueNode> specified. This is not valid.
                _logger.Error("(Natvis) Content of element <LinkedListItems> is incomplete: " +
                              "no <ValueNode> found.");
                return false;
            }

            return await HandleFormattableStringAsync(listItemsNode.Name, context) &&
                   await HandleExpressionAsync(listItemsNode.Value, context);
        }

        /// <summary>
        /// Handles <TreeItems> element.
        ///
        /// Possible values:
        ///  <Size>
        ///  <HeadPointer>
        ///  <LeftPointer>
        ///  <RightPointer>
        ///  <ValueNode>
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleTreeItemsAsync(TreeItemsType treeItems, Context context)
        {
            if (treeItems.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(treeItems.Size, context) &&
                   await HandleExpressionAsync(treeItems.Condition, context) &&
                   await HandleExpressionAsync(treeItems.HeadPointer, context) &&
                   await HandleExpressionAsync(treeItems.LeftPointer, context) &&
                   await HandleExpressionAsync(treeItems.RightPointer, context) &&
                   await HandleTreeItemsNodeAsync(treeItems.ValueNode, context);
        }

        /// <summary>
        /// Handles <ValueNode> (direct child of <TreeItems>).
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Condition
        ///  Name (formattable)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleTreeItemsNodeAsync(TreeItemsNodeType treeItemsNode, Context context)
        {
            if (treeItemsNode == null)
            {
                // The value is null if there's no <ValueNode> specified. This is not valid.
                _logger.Error("(Natvis) Content of element <TreeItems> is incomplete: " +
                              "no <ValueNode> found.");
                return false;
            }

            return await HandleFormattableStringAsync(treeItemsNode.Name, context) &&
                   await HandleExpressionAsync(treeItemsNode.Condition, context) &&
                   await HandleExpressionAsync(treeItemsNode.Value, context);
        }

        /// <summary>
        /// Handles <ExpandedItem> element.
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleExpandedItemAsync(ExpandedItemType expandedItem, Context context)
        {
            if (expandedItem.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(expandedItem.Condition, context) &&
                   await HandleExpressionAsync(expandedItem.Value, context);
        }

        /// <summary>
        /// Handles <Synthetic> element.
        ///
        /// Possible child elements:
        ///  <DisplayString>
        ///  <StringView>
        ///  <ExpandType>
        ///  <CustomVisualizer>  (not supported)
        ///
        /// Possible attributes:
        ///  Name
        ///  Expression
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleSyntheticItemAsync(SyntheticItemType syntheticItem, Context context)
        {
            if (syntheticItem.Optional)
            {
                return true;
            }

            return await HandleDisplayStringGroupAsync(syntheticItem.DisplayString, context) &&
                   await HandleStringViewGroupAsync(syntheticItem.StringView, context) &&
                   await HandleExpressionAsync(syntheticItem.Condition, context) &&
                   await HandleExpandAsync(syntheticItem.Expand, context) &&
                   await HandleExpressionAsync(syntheticItem.Expression, context);
        }

        /// <summary>
        /// Handles <CustomListItems> element.
        ///
        /// Possible child nodes:
        ///  <Variable>
        ///  <Size>
        ///  <Skip>
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes:
        ///  MaxItemsPerView
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleCustomListItemsAsync(CustomListItemsType customListItems,
                                                    Context context)
        {
            if (customListItems.Optional)
            {
                return true;
            }

            // Clone context. Context variables can be defined in this scope.
            context = context.Clone();
            // Handle <Variable>s first. This will initialize the context for remaining elements.
            if (!await HandleVariableGroupAsync(customListItems.Variable, context))
            {
                return false;
            }

            return await HandleExpressionAsync(customListItems.Condition, context) &&
                   await HandleSizeGroupAsync(customListItems.Size, context) &&
                   await HandleCodeBlockAsync(customListItems.CodeBlock, context);
        }

        /// <summary>
        /// Handles a group of <Variable> elements.
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleVariableGroupAsync(VariableType[] variableGroup, Context context)
        {
            if (variableGroup == null)
            {
                // The array is null if there are no <Variable> elements.
                return true;
            }

            foreach (var variable in variableGroup)
            {
                if (!await HandleVariableAsync(variable, context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles a group of "CustomListItems code block" elements:
        ///  <Item>
        ///  <Loop>
        ///  <If>
        ///  <Elseif>
        ///  <Else>
        ///  <Exec>
        ///  <Break>
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleCodeBlockAsync(object[] blockItems, Context context)
        {
            if (blockItems == null)
            {
                // The array is null if there are no items in CustomListItems. This is valid.
                return true;
            }

            // Handle all code blocks.
            foreach (var obj in blockItems)
            {
                if (!await HandleCodeBlockOptionAsync(obj, context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles a single unnamed option of "CustomListItems code".
        /// </summary>
        async Task<bool> HandleCodeBlockOptionAsync(object obj, Context context)
        {
            if (obj is CustomListItemType customListItem)
            {
                return await HandleCustomListItemAsync(customListItem, context);
            }
            else if (obj is LoopType loop)
            {
                return await HandleLoopAsync(loop, context);
            }
            else if (obj is IfType ifType)
            {
                return await HandleIfAsync(ifType, context);
            }
            else if (obj is ElseType elseType)
            {
                return await HandleElseAsync(elseType, context);
            }
            else if (obj is ElseifType elseif)
            {
                return await HandleElseifAsync(elseif, context);
            }
            else if (obj is ExecType exec)
            {
                return await HandleExecAsync(exec, context);
            }
            else if (obj is BreakType breakType)
            {
                return await HandleBreakAsync(breakType, context);
            }

            Debug.Fail("All CustomListItems code block options are exhausted.");
            return false;
        }

        /// <summary>
        /// Handles <Loop> element.
        ///
        /// Possible child elements:
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleLoopAsync(LoopType loop, Context context)
        {
            return await HandleExpressionAsync(loop.Condition, context) &&
                   await HandleCodeBlockAsync(loop.CodeBlock, context);
        }

        /// <summary>
        /// Handles <If> element.
        ///
        /// Possible child elements:
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleIfAsync(IfType ifType, Context context)
        {
            return await HandleExpressionAsync(ifType.Condition, context) &&
                   await HandleCodeBlockAsync(ifType.CodeBlock, context);
        }

        /// <summary>
        /// Handles <Else> element.
        ///
        /// Possible child elements:
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes: none.
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleElseAsync(ElseType elseType, Context context)
        {
            return await HandleCodeBlockAsync(elseType.CodeBlock, context);
        }

        /// <summary>
        /// Handles <Elseif> element.
        ///
        /// Possible child elements:
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleElseifAsync(ElseifType elseif, Context context)
        {
            return await HandleExpressionAsync(elseif.Condition, context) &&
                   await HandleCodeBlockAsync(elseif.CodeBlock, context);
        }

        /// <summary>
        /// Handles <Exec> element.
        ///
        /// Possible child elements:
        ///  "CustomListItems code elements"
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleExecAsync(ExecType exec, Context context)
        {
            return await HandleExpressionAsync(exec.Condition, context) &&
                   await HandleExpressionAsync(exec.Value, context);
        }

        /// <summary>
        /// Handles <Break> element.
        ///
        /// Possible child elements: none.
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleBreakAsync(BreakType breakType, Context context)
        {
            return await HandleExpressionAsync(breakType.Condition, context);
        }

        /// <summary>
        /// Handles <Item> element (direct child of <CustomListItems>).
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Name (formattable)
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleCustomListItemAsync(CustomListItemType customListItem,
                                                   Context context)
        {
            return await HandleFormattableStringAsync(customListItem.Name, context) &&
                   await HandleExpressionAsync(customListItem.Condition, context) &&
                   await HandleExpressionAsync(customListItem.Value, context);
        }

        /// <summary>
        /// Handles <Variable> element.
        ///
        /// Child elements: none.
        ///
        /// Possible attributes:
        ///  Name
        ///  InitialValue
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleVariableAsync(VariableType variable, Context context)
        {
            var sbType = await CompileExpressionAsync(variable.InitialValue, context);
            if (sbType != null)
            {
                // Redefinition of the same name more than once is allowed.
                context.Arguments.Add(variable.Name, sbType);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Handles <Size> element.
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  "Common attributes" (Condition, Optional, view constraints, version constraints)
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleSizeAsync(SizeType size, Context context)
        {
            if (size.Optional)
            {
                return true;
            }

            return await HandleExpressionAsync(size.Condition, context) &&
                   await HandleExpressionAsync(size.Value, context);
        }

        /// <summary>
        /// Handles <ValueNode> element (direct child of <IndexListItems>).
        ///
        /// Child element:
        ///  expression string
        ///
        /// Possible attributes:
        ///  Condition
        /// </summary>
        /// <return>Boolean value indicating correctness of non-optional expressions.</return>
        async Task<bool> HandleValueNodeAsync(IndexNodeType valueNode, Context context)
        {
            return await HandleExpressionAsync(valueNode.Condition, context) &&
                   await HandleExpressionAsync(valueNode.Value, context);
        }

        /// <summary>
        /// Handles a single expression (including possible formatting options, e.g. ",5s").
        /// </summary>
        /// <returns>Boolean value indicating the correctness of the expression.</returns>
        async Task<bool> HandleExpressionAsync(string expr, Context context)
        {
            if (expr == null || expr == "")
            {
                return true;
            }

            VsExpression vsExpression;
            try
            {
                // Split expression and try to compile size specifier.
                vsExpression = await _vsExpressionCreator.CreateAsync(
                    expr, sizeExpr => CompileSizeSpecifierAsync(sizeExpr, context));
            }
            catch (ExpressionEvaluationFailed ex)
            {
                _logger.Error($"(Natvis) Failed to compile size specifier: {ex.Message}");
                return false;
            }

            // Compile the main expression.
            expr = NatvisExpressionEvaluator.ReplaceScopedNames(vsExpression.Value,
                                                                context.ScopedNames);

            return await CompileExpressionAsync(expr, context) != null;
        }

        /// <summary>
        /// Compiled a simple expression (without formatting extensions).
        /// </summary>
        /// <returns>Type of result, or null in the case of error.</returns>
        async Task<SbType> CompileExpressionAsync(string expr, Context context)
        {
            (SbType type, SbError error) =
                await _target.CompileExpressionAsync(context.Scope, expr, context.Arguments);

            if (error != null && error.Fail())
            {
                _logger.Error($"(Natvis) Error while parsing expression '{expr}' in the context " +
                              $"of type '{context.Scope.GetName()}': {error.GetCString()}");
                return null;
            }

            if (string.IsNullOrEmpty(type?.GetName()))
            {
                // Type is invalid, but error isn't set. This isn't expected to happen.
                _logger.Warning($"(Natvis) Received null result while parsing expression " +
                                $"'{expr}' in the context of type '{context.Scope.GetName()}'.");
                return null;
            }

            return type;
        }

        async Task<uint> CompileSizeSpecifierAsync(string expr, Context context)
        {
            var result = await CompileExpressionAsync(expr, context);
            if (result == null)
            {
                throw new ExpressionEvaluationFailed($"invalid expression '{expr}'");
            }
            if (!IsIntegerType(result))
            {
                throw new ExpressionEvaluationFailed($"'{expr}' isn't integer");
            }
            // Any integer represents success.
            return 0;
        }
    }
}
