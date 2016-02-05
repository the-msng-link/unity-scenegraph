using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System.Reflection;
using System;
using Object = UnityEngine.Object;

//flow:
//- Locate all gameobjects (root nodes)
//- Locate all components  (second level)  
//- Locate all properties  (third level)
//- Locate all connections
//- Layout nodes
//- - Sort nodes by number of OUTPUTS
//- - Highest number of outputs correlates
//- DRAW all connections
//- Draw all Nodes
//Graph
//Starts with a root node
//create connections
//a connection is simply a leaf node (field reference) that is a UObject, so we CAN know

//so we need a way of finding connections
public class SceneMapWindow : EditorWindow {
	Node[] rootNodes;
	Node[] allNodes;
	Dictionary<Node, Rect> nodeViewRects = new Dictionary<Node, Rect> ();
	public bool drawRootsWithoutConnections;
	public bool drawSiblingConnections;
	public bool drawAnyWithoutConnections;

	[MenuItem("Window/Scene Map")]
	static void Open()
	{
		GetWindow<SceneMapWindow> ();
	}

	Vector2 scrollPosition = Vector2.zero;

	[MenuItem ("GameObject/View on Scene Map...", priority = 10)]
	static void AnalyzeGoGraph () {
		GameObject go = Selection.activeGameObject;
		var window = GetWindow<SceneMapWindow> ();
		var selectionNode = window.allNodes.FirstOrDefault (x => x.context == go);
		if (selectionNode != null)
			window.SetFirstVisibleNode (selectionNode);
	}

	void SetFirstVisibleNode(Node node)
	{
		foreach(var current in rootNodes) current.isVisible = false;

		node.isVisible = true;
		CalculateLayout ();
	}

	//find all connections and child connections for each root node
	//and make the root nodes of such connections visible
	public void DigForConnections()
	{
		var allVisible = rootNodes.Where (x => x.isVisible);	//find all currently visible nodes
		var allVisibleConnections = allVisible.SelectMany(x => x.IncomingConnectionsRecursive)
			.Concat(allVisible.SelectMany(x => x.CurrentAndChildConnections));
		Debug.Log ("all count: " + string.Join(",", allVisibleConnections.Select(x => x.title).ToArray()));
		foreach (var connection in allVisibleConnections)
			connection.Root.isVisible = true;

		CalculateLayout ();
	}

	void OnEnable()
	{
		this.titleContent = new GUIContent ("Graph", AssetDatabase.LoadAssetAtPath<Texture2D> ("Assets/Editor/Graph_Icon.png"));

		FindNodes ();
	}

	void FindNodes(){
		rootNodes = Resources.FindObjectsOfTypeAll<GameObject> ()
			.Select (x => new Node {  context = x,
			title = x.name + " (GameObject)",
			children = x.GetComponents<MonoBehaviour> ()
												.Where (mb => mb != null)
												.Select (mb => new Node { 	context = mb,
				title = mb.GetType ().ToString () + " (Component)",
				children = mb.GetType ().GetFields (BindingFlags.Public | BindingFlags.Instance)
																									.Where (field => field.FieldType.IsSubclassOf (typeof(Component)))
																									.Select (field => new Node {
					context = field,
					title = field.Name + " (" + field.FieldType.Name + ")"
				})
																								.ToArray ()
			}).ToArray ()
		})
			.ToArray ();

		allNodes = rootNodes.SelectMany(x => x.NodeAndChildrenRecursive).ToArray ();
		foreach (var node in allNodes) {
			if (!node.IsLeaf) {
				foreach (var child in node.children) {
					child.parent = node;
				}
			}
		}

		//for our purposes, only leaves (leaves are fields, connections represent references)
		//so find all the leaves
		//then find all the connections
		var allLeaves = allNodes.Where (x => x.IsLeaf);
		foreach (var leaf in allLeaves) {
			if (leaf.context is FieldInfo) {
				var fieldInfo = leaf.context as FieldInfo;
				if(fieldInfo.FieldType.IsSubclassOf(typeof(Component)))
				{
					//we need to grab the VALUE of this field on the target object (Which should be the parent node)
					//and then, if it's non-null, find a scene reference to it
					//THEN find the node that corresponds to that scene reference
					//and THEN we can add a connection

					var fieldValue = fieldInfo.GetValue(leaf.parent.context);
					if (fieldValue != null) {
						var connectionTargetNode = allNodes.FirstOrDefault (x => x.context == fieldValue);
						if (connectionTargetNode == null) {
						}
						else {
							if (leaf.connections == null)
								leaf.connections = new List<Node> ();
							if (connectionTargetNode.incomingConnections == null)
								connectionTargetNode.incomingConnections = new List<Node> ();

							connectionTargetNode.incomingConnections.Add (leaf);
							leaf.connections.Add (connectionTargetNode);
						}
					}
				}
			}
		}

		//so now we've got all our connections setup, let's order root nodes by connection count
		rootNodes = rootNodes.OrderByDescending (rootNode => rootNode.HasOutputsRecursive ? rootNode.CurrentAndChildConnections.Length : 0)
			.ToArray ();

	}

	GUISkin skin;
	void OnGUI()
	{
		if (rootNodes == null)
			return;

		if (skin == null)
			skin = AssetDatabase.LoadAssetAtPath<GUISkin> ("Assets/Editor/GraphArt/Graph_Skin.guiskin");

		DrawGrid ();
		DrawToolbar ();
		GUI.skin = skin;
		//start scrollview
		DrawConnections ();
		DrawNodes ();
		//end scrollview
	}

	void DrawGrid()
	{
		var pos = this.position;
		pos.x = pos.y = 0.0f;
		GUI.Box (pos, "", skin.GetStyle("Grid"));
	}

	void DrawToolbar()
	{
		GUILayout.BeginHorizontal(GUILayout.Height (20));
		{
			if (GUILayout.Button ("Grab Selection", GUILayout.Width(100))) {
				var selectionNode = allNodes.FirstOrDefault (x => x.context == Selection.activeGameObject);
				Debug.Log ("Selection: " + Selection.activeGameObject);
				Debug.Log ("Node: " + selectionNode);
				if (selectionNode != null)
					SetFirstVisibleNode (selectionNode);
			}

			if (GUILayout.Button ("Dig", GUILayout.Width(100)))
				DigForConnections ();
		}
		GUILayout.EndVertical ();
	}

	void DrawNodes()
	{
		foreach (var node in rootNodes) {
			if (node == null) {
				Debug.Log ("encountered null node");
				continue;
			}

			if (!node.isVisible)
				continue;
			if (node.HasOutputsRecursive || node.HasInputsRecursive || drawRootsWithoutConnections) {
				DrawNode (node);
			}
		}
	}

	static GUIStyle foldoutStyle;
	void DrawNode(Node node, int depth = 0)
	{
		
		var style = skin.GetStyle ("Node" + depth);
		if (node.IsLeaf) {
			if (GUI.Button (CalculateAbsoluteRectForNode(node), node.title, style))
			if (node.context is FieldInfo) {
				EditorGUIUtility.PingObject ((node.context as FieldInfo).GetValue (node.parent.context) as Object);
			}
		} else if (node.IsRoot) {
			if (DragButton (nodeViewRects[node], newRect => nodeViewRects[node] = newRect, node.title, node, style)) {
				node.isExpanded = !node.isExpanded;
				CalculateExpandedNode (node);
			}
		} 
		else 
		{
			var wasExpanded = node.isExpanded;
			node.isExpanded = GUI.Toggle (CalculateAbsoluteRectForNode(node), node.isExpanded, node.title, style);
			if (wasExpanded != node.isExpanded)
				CalculateExpandedNode (node);
		}

		if (node.isExpanded && !node.IsLeaf) {
			foreach (var child in node.children) {
				if(child.HasInputsRecursive || child.HasOutputsRecursive || drawAnyWithoutConnections)
					DrawNode (child, depth + 1);
			}
		}
	}

	float dragLength = 0.0f;
	object draggingButtonContext = null;
	bool DragButton(Rect drawRect, Action<Rect> newRectSetter, string title, object context, GUIStyle style = null)
	{
		if (Event.current.type == EventType.MouseDown) {
			dragLength = 0.0f;
			draggingButtonContext = context;
		} else if (Event.current.type == EventType.MouseUp) {
			draggingButtonContext = null;
		}
		if (Event.current.type == EventType.MouseDrag && context == draggingButtonContext && context != null) {
			drawRect.x += Event.current.delta.x;
			drawRect.y += Event.current.delta.y;
			dragLength += Mathf.Abs(Event.current.delta.x) + Mathf.Abs(Event.current.delta.y);
		}

		newRectSetter (drawRect);
		bool clicked = GUI.Button (drawRect, title, style);

		return dragLength > 1.0f ? false : clicked;
	}

	void DrawConnections()
	{
		if (Event.current.type != EventType.Repaint)
			return;
		foreach (var node in rootNodes) {
			if(node.IsRootVisible)
				DrawConnectionsForNode (node);
		}
	}

	void DrawConnectionsForNode(Node rootNode)
	{
		if (rootNode.isExpanded) {
			foreach (var child in rootNode.children) {
				DrawConnectionsForNode (child);
			}
		} else {
			//this node is NOT expanded - we need to draw all child connections through us
			var allChildConnections = rootNode.CurrentAndChildConnections;
			if (allChildConnections != null)
				foreach (var childConn in allChildConnections) {
					if(childConn.IsRootVisible)
						if(drawSiblingConnections || childConn.parent != rootNode.parent)
							DrawLineBetweenNodes (rootNode, childConn.HighestAncestorWithExpandedParentOrRoot);
				}
		}

		//draw this node's specific connections
		if (rootNode.HasOutputs) {
			foreach (var connection in rootNode.connections) {
				if(connection.IsRootVisible)
					if(drawSiblingConnections || connection.parent != rootNode.parent)
						DrawLineBetweenNodes (rootNode, connection.HighestAncestorWithExpandedParentOrRoot, Color.green);
			}
		}
	}

	void DrawLineBetweenNodes(Node n1, Node n2, Color? color = null)
	{
		var n1Rect = CalculateAbsoluteRectForNode(n1);
		var n2Rect = CalculateAbsoluteRectForNode(n2);
		var isOnSameLine = Mathf.Abs(n2Rect.x - n1Rect.x) < 40f;
		var side = Mathf.Sign(n2Rect.x - n1Rect.x);

		var n1Point = n1Rect.center + Vector2.right * n1Rect.width * 0.5f * side;
		var n2Point = n2Rect.center - Vector2.right * n2Rect.width * 0.5f * side;
		if(isOnSameLine)
			n2Point = n2Rect.center + Vector2.right * n2Rect.width * 0.5f * side;
		
		Vector2 startHandle = n1Point;
		startHandle.x = n2Point.x;
		Vector2 endHandle = n2Point;
		endHandle.x = n1Point.x;

		Drawing.DrawBezierLine (n1Point, startHandle, n2Point, endHandle, Color.black * 0.5f, 3.0f, true, 12);
		Drawing.DrawLine (n2Point, n2Point + Vector2.left * 5f * side + Vector2.up * 3f, Color.black * 0.5f, 2.0f, true);
		Drawing.DrawLine(n2Point, n2Point + Vector2.left * 5f * side - Vector2.up * 3f, Color.black * 0.5f, 2.0f, true);


		Drawing.DrawBezierLine (n1Point, startHandle, n2Point, endHandle, color == null ? Color.white : color.Value, 1.0f, true, 24);
		Drawing.DrawLine(n2Point, n2Point + Vector2.left * 5f * side + Vector2.up * 3f, color == null ? Color.white : color.Value, 1.0f, true);
		Drawing.DrawLine(n2Point, n2Point + Vector2.left * 5f * side - Vector2.up * 3f, color == null ? Color.white : color.Value, 1.0f, true);
	}

	static private int[] NodeLevelHeights = new[]{ 24, 20, 20, 20, 20 };

	int CalculateHeightForNode(Node node, int currentLevel = 0)
	{
		if (!node.isExpanded || node.IsLeaf)
			return NodeLevelHeights [currentLevel];
		int nodeHeight = NodeLevelHeights [currentLevel];
		foreach (var child in node.children) {
			nodeHeight += CalculateHeightForNode (child, currentLevel + 1);
		}
		return nodeHeight;
	}

	Rect CalculateBoundsForNode(Node node, int currentLevel = 0)
	{
		var height = CalculateHeightForNode (node);
		var width = 200f;
		return new Rect (0, 0, width, height);
	}

	void CalculateExpandedNode(Node startNode)
	{
		LayoutChildrenRecursive (startNode, nodeViewRects);
	}

	void CalculateLayout()
	{
		var nodes = rootNodes.Where(x => x.isVisible);
		int[] columnsMinY = new int[4];
		int column = 0;

		//first, place the roots
		foreach (var node in nodes) {
			var bounds = CalculateBoundsForNode (node);
			bounds.x = column * 220f;
			bounds.y = 30f + columnsMinY [column];
			bounds.height = NodeLevelHeights [0];
			nodeViewRects [node] = bounds;
			columnsMinY [column] += (int)bounds.height + 20;

			LayoutChildrenRecursive (node, nodeViewRects);
			column = (column + 1) % 4;
		}
	}

	void LayoutChildrenRecursive(Node parent, Dictionary<Node, Rect> layout)
	{
		if (!parent.isExpanded)
			return;

		var parentRect = layout [parent];
		float currentHeight = layout [parent].height;
		foreach (var child in parent.children) {
			layout[child] = new Rect(0f, currentHeight, layout[parent].width, NodeLevelHeights[child.Depth]);
			currentHeight += NodeLevelHeights [child.Depth];
			//so we've laid out the child... now need to lay out the child's children
			LayoutChildrenRecursive(child, layout);
		}
	}

	Rect CalculateAbsoluteRectForNode(Node node)
	{
		if (node.IsRoot)
			return nodeViewRects [node];

		return nodeViewRects [node].Add(CalculateAbsoluteRectForNode(node.parent).min);
	}
}

static public class RectUtilities
{
	static public Rect Add(this Rect r, Vector2 offsetMinBy)
	{
		return new Rect (r.x + offsetMinBy.x, r.y + offsetMinBy.y, r.width, r.height);
	}
}