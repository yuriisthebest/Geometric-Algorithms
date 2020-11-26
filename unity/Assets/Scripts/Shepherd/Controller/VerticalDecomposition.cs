using UnityEngine;
using System.Collections.Generic;

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

    }

    void Insert(LineSegment seg)
    {

    }

    Trapezoid Search(Vector2 p)
    {

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
    public var face;

    /*
     * Create a line reprented by two points
     */
    public LineSegment(Vector2 p1, Vector2 p2, var shepherd)
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
    public var store;
    public var leftChild;
    public var rightChild;
}