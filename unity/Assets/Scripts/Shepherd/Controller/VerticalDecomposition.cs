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
        // TODO, Initialize vertical decomposition with one trapezoid containing the entire canvas

        // Transform all DCEL edges into Linesegments
        ICollection<HalfEdge> edges = InGraph.Edges;
        // TODO Pick a random segment (or randomize the list of halfedges before adding)
        foreach (HalfEdge edge in edges)
        {
            // Only add half-edges whose face is above it
            //  So only add half-edges whose to-vertex is to the right of the from-vertex
            //  So skip half-edges where 'to' has smaller x value than 'from'
            if (edge.To.Pos.x < edge.From.Pos.x) { continue; }
            if (edge.To.Pos.x == edge.From.Pos.x && edge.To.Pos.y <= edge.From.Pos.y) { continue; }
            LineSegment segment = new LineSegment(edge.From.Pos, edge.To.Pos, edge.Face);
            this.Insert(segment);
        }
    }

    /*
     * Inserting a segment to the vertical decomposition
     * 
     * Find all existing trapezoids intersecting the segment
     * Modify all these trapezoids to use the inserted segment (study slide 31-7 to see most interactions)
     * Find all leaves that point to changed trapezoids (need to have pointer from trapezoid to leaf?)
     * Make new leafs for the new trapezoids
     * Old leafs become inner nodes
     */
    void Insert(LineSegment seg)
    {
        // Find all trapezoids intersecting the segment
        List<Trapezoid> Intersecting = this.FindIntersecting(seg);
        // Find all leaves of changed 
        List<SearchNode> OldLeafs = new List<SearchNode>();
        foreach (Trapezoid trap in Intersecting)
        {
            // Trapezoids should always point to leafs
            if (!trap.leaf.isLeaf) { throw new System.Exception(); }
            OldLeafs.Add(trap.leaf);
        }
        // Modify all intersecting trapezoids, by deleting, merging and creating trapezoids TODO
        // First split trapezoids in two, three or four trapezoids, then merge trapezoids

        // Create new leafs for the new trapezoids TODO

        // Change old leafs and add inner nodes that point to new leafs TODO
    }

    /*
     * Given a point position, return the trapezoid that point is contained
     */
    public Trapezoid Search(Vector2 pos, SearchNode root)
    {
        SearchNode node = root.Test(pos);
        if (node.isLeaf)
        {
            return node.leaf;
        }
        return this.Search(pos, node);
    }


    /*
     * Return all trapezoids that are intersecting a given segment
     *  Assumes that the segment does not cross any segments (VD assumption)
     *  
     * DEGENERATE CASE WARNING
     */
    private List<Trapezoid> FindIntersecting(LineSegment seg)
    {
        // Find all trapezoids intersecting the segment
        List<Trapezoid> Intersecting = new List<Trapezoid>();
        Trapezoid CurrentTrapezoid = this.Search(seg.point1, this.TreeRoot);
        Intersecting.Add(CurrentTrapezoid);
        while (seg.point2.x > CurrentTrapezoid.right.x)
        {
            // Check neighbors to find if there are one or two neighbors, will return a list with one or two entries
            List<Trapezoid> RightNeighbors = CurrentTrapezoid.Neighbors.FindAll(
                delegate (Trapezoid trap)
                {
                    return !(trap.right.x < CurrentTrapezoid.right.x);
                });
            // If there is only one neighbor, it is the new trapezoid, else check which one is
            if (RightNeighbors.Count == 1)
            {
                CurrentTrapezoid = RightNeighbors[0];
            }
            else
            {
                if (RightNeighbors.Count != 2) { throw new System.IndexOutOfRangeException(); }
                // Report upper or lower, and find which one it is
                // If the segment is above the right point of the current trapezoid, then the next trapezoid is the above neighbor, else the lower neighbor
                if (seg.Above(CurrentTrapezoid.right))
                {
                    // If the left point of the bottom segment is equal to the right point of current, then it is upper neighbor. Except if there is only one neighbor
                    if (RightNeighbors[0].bottom.point1 == CurrentTrapezoid.right)
                    {
                        // Segment is above the point and first neighbor is upper neighbor
                        CurrentTrapezoid = RightNeighbors[0];
                    }
                    else
                    {
                        // Segment is above the point and second neighbor is upper neighbor
                        CurrentTrapezoid = RightNeighbors[1];
                    }
                }
                else
                {
                    // If the left point of top segment is equal to the right point of current, then it is lower neighbor. Except if there is only one neighbor
                    if (RightNeighbors[0].top.point1 == CurrentTrapezoid.right)
                    {
                        // Segment is below the point and first neighbor is lower neighbor
                        CurrentTrapezoid = RightNeighbors[0];
                    }
                    else
                    {
                        // Segment is below the point and second neighbor is lower neighbor
                        CurrentTrapezoid = RightNeighbors[1];
                    }
                }
            }
            Intersecting.Add(CurrentTrapezoid);
        }
        return Intersecting;
    }

}

public class Trapezoid
{
    public LineSegment top;
    public LineSegment bottom;
    public Vector2 left;
    public Vector2 right;
    public List<Trapezoid> Neighbors;
    public SearchNode leaf;

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
 *  point1 has a smaller x-coord than point2
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
     *  point1 is always left of point2
     */
    public LineSegment(Vector2 p1, Vector2 p2, Face shepherd)
    {
        point1 = (p1.x <= p2.x) ? p1 : p2;
        point2 = (p1.x > p2.x) ? p1 : p2;
        face = shepherd;
    }

    /*
     * Return whether a given point is above (and not on) the segment
     * Return false is the point is not directly below, above or on the segment
     * 
     * Find left point of segment
     * Determine slope of segment                           (slope = diffY / diffX)
     * Interpolate y of segment at x pos of given point     (below = pos.y < (pos.x - smallest x) * slope + y of smallest x)
     * Test if the y of the point is above
     */
    public bool Above(Vector2 point)
    {
        if (this.point1.x > point.x || this.point2.x < point.x) { return false; }
        float slope = (this.point1.y - this.point2.y) / (this.point1.x - this.point2.x);
        return (point.y > this.point1.y + (point.x - this.point1.x) * slope);
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
            // If point is right, or on top of endpoint return right child (See slide 36 / 129)
            if (pos.x < storeX.x)
            {
                return leftChild;
            }
            return rightChild;
        }
        else
        {
            // TODO LineSegment now has this test as a method (above = true, below / on = false), don't know if I want to use that one
            // If point is below the segment, return left
            //  slope = diffY / diffX
            //  below = pos.y < (pos.x - smallest x) * slope + y of smallest x
            float slope = (storeY.point1.y - storeY.point2.y) / (storeY.point1.x - storeY.point2.x);
            if (pos.y < storeY.point1.y + (pos.x - storeY.point1.x) * slope)
            {
                return leftChild;
            } else
            {
                return rightChild;
            }
        }
    }
}