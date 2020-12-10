using UnityEngine;
using System.Collections.Generic;
using Util.Geometry.DCEL;

public class VerticalDecomposition
{
    List<Trapezoid> traps;
    SearchNode TreeRoot;
    
    /*
     * Given an Voronoi diagram in DCEL format:
     *  Extract all edges and map them to LineSegments
     *  Store with each segment the shepherd above it
     *  Incrementally add each segment to the vertical decomposition
     */
    public VerticalDecomposition(DCEL InGraph)
    {
        // Transform all DCEL edges into Linesegments
        ICollection<HalfEdge> edges = InGraph.Edges;
        foreach (HalfEdge edge in edges)
        {
            // Only add half-edges whose face is above it
            //  So only add half-edges whose to-vertex is to the right of the from-vertex
            //  So skip half-edges where 'to' has smaller x value than 'from'
            if (edge.To.Pos.x < edge.From.Pos.x) { continue; }
            LineSegment segment = new LineSegment(edge.From.Pos, edge.To.Pos, edge.Face);
            this.Insert(segment);
        }
    }

    void Insert(LineSegment seg)
    {

    }

    /*
     * Given a point position, return the trapezoid that point is contained in
     * TODO, change return from trepezoid to color of face -> requires Trapzeoid.bottom.Face (and Annes permission)
     */
    Trapezoid Search(Vector2 pos, SearchNode root)
    {
        SearchNode node = root.Test(pos);
        if (node.isLeaf)
        {
            return node.leaf;
        }
        return this.Search(pos, node);
    }

}

public class Trapezoid
{
    public LineSegment top;
    public LineSegment bottom;
    public Vector2 left;
    public Vector2 right;
    public List<Trapezoid> neighbors;

    /* 
     * Create a trapezoid represented by two linesegments above and below, and two points left and right
     */
    public Trapezoid(Vector2 l, Vector2 r, LineSegment t, LineSegment b)
    {
        top = t;
        bottom = b;
        left = l;
        right = r;
    }
}

/*
 * A linesegment is represented by it's two vertices
 * Additionally, a linesegment stores the face directly above the segment
 *  This is a shepherd in our case
 */
public class LineSegment
{
    public Vector2 point1;
    public Vector2 point2;
    public Face face;

    /*
     * Create a line reprented by two points
     */
    public LineSegment(Vector2 p1, Vector2 p2, Face shepherd)
    {
        point1 = p1;
        point2 = p2;
        face = shepherd;
    }

}

/*
 * A single node in the vertical decomposition search tree
 * 
 * Any node is either an X or Y node.
 *  X nodes store an endpoint and tests if a point is to the left or right
 *  Y nodes store a segment and tests if a point is below or above
 * Points to a left and right child, which can be either Trapezoids or other SearchNodes
 */
public class SearchNode
{
    public bool isXNode;
    public bool isLeaf;
    public Vector2 storeX;
    public LineSegment storeY;
    public SearchNode leftChild;
    public SearchNode rightChild;
    public Trapezoid leaf;


    /*
     * Return the left or right child of the node depending a given position and the stored component
     */
    public SearchNode Test(Vector2 pos)
    {
        if (isXNode)
        {
            // If point is left of endpoint, return left child.
            // Otherwise return right child
            if (pos.x <= storeX.x)
            {
                return leftChild;
            }
            return rightChild;
        }
        else
        {
            // If point is below the segment, return left
            //  slope = diffY / diffX
            //  below = pos.y < (pos.x - smallest x) * slope + y of smallest x
            Vector2 smallest = (storeY.point1.x < storeY.point2.x) ? storeY.point1 : storeY.point2;
            float slope = (storeY.point1.y - storeY.point2.y) / (storeY.point1.x - storeY.point2.x);
            if (pos.y < smallest.y + (pos.x - smallest.x) * slope)
            {
                return leftChild;
            } else
            {
                return rightChild;
            }
        }
    }
}