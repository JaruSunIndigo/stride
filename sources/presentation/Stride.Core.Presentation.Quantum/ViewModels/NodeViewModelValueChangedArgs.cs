// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Core.Presentation.Quantum.ViewModels;

public class NodeViewModelValueChangedArgs : EventArgs
{
    public NodeViewModelValueChangedArgs(GraphViewModel viewModel, NodeViewModel node)
    {
        ViewModel = viewModel;
        Node = node;
    }

    public GraphViewModel ViewModel { get; }

    public NodeViewModel Node { get; }
}
