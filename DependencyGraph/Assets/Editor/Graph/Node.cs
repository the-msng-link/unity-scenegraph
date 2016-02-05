using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


public class Node
{
	//for a GO node, children will be components
	//for component nodes, children will be properties
	public string title;
	public object context;
	public Node Root { get{ return parent == null ? this : parent; } }
	public Node parent;
	public Node[] children;
	public List<Node> incomingConnections;
	public List<Node> connections;
	public bool IsLeaf { get { return children == null || children.Length == 0; } }
	public bool HasOutputs { get { return connections != null && connections.Count > 0; } }
	public bool HasInputs { get { return incomingConnections != null && incomingConnections.Count > 0; } }
	public bool HasOutputsRecursive 
	{ 
		get 
		{
			if (HasOutputs)
				return true;

			//we don't have outputs, but our children might!

			if (IsLeaf)
				return HasOutputs;

			return children.Any (x => x.HasOutputsRecursive);
		} 
	}

	public bool HasInputsRecursive 
	{ 
		get 
		{
			if (HasInputs)
				return true;

			//we don't have outputs, but our children might!

			if (IsLeaf)
				return HasInputs;

			foreach (var child in children) {
				if (child.HasInputsRecursive)
					return true;
			}

			return false;
		} 
	}

	//REFACTOR: move to view class
	public bool isVisible;		//only valid on root nodes
	//REFACTOR: move to view class
	public bool IsRootVisible { get { return Root.isVisible; } }

	public bool IsRoot { get { return parent == null; } }

	//REFACTOR: move to view class
	public bool isExpanded;
	public int Depth { get { return IsRoot ? 0 : parent.Depth; } }
	//public event System.Action<Node> OnClick;

	//REFACTOR: move to View extension
	public bool IsVisibleInTree
	{
		get 
		{ 
			if (parent == null)
				return isVisible;
			if (!parent.isExpanded)
				return false;

			return parent.IsVisibleInTree; 
		}
	}

	//REFACTOR: Move to View extension
	public bool IsExpandedToRoot { get { return IsRoot ? isExpanded : parent.IsExpandedToRoot; } }

	public Node[] CurrentAndChildConnections
	{
		get {
			if (IsLeaf)
				return connections == null ? null : connections.ToArray();

			return NodeAndChildrenRecursive.Where(x => x.HasOutputs)
				.SelectMany (x => x.connections)
				.ToArray();
		}
	}

	public Node[] IncomingConnectionsRecursive
	{
		get {
			if (IsLeaf)
				return incomingConnections == null ? null : incomingConnections.ToArray();

			return NodeAndChildrenRecursive.Where(x => x.HasInputs)
				.SelectMany (x => x.incomingConnections)
				.ToArray();
		}
	}

	public IEnumerable<Node> NodeAndChildrenRecursive
	{
		get 
		{
			if (IsLeaf)
				return new[]{ this };
			return new[]{this}.Concat(children.SelectMany(x => x.NodeAndChildrenRecursive)).ToArray();
		}
	}

	public Node HighestAncestorWithExpandedParentOrRoot
	{
		get {
			if (isExpanded || parent == null || parent.isExpanded)
				return this;
			else
				return parent.HighestAncestorWithExpandedParentOrRoot;
		}
	}

}
