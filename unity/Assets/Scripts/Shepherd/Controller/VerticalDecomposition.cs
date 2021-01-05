using UnityEngine;
using System.Collections.Generic;
using Util.Geometry.DCEL;

public class VerticalDecomposition
{
    public List<Trapezoid> traps = new List<Trapezoid>();
    SearchNode TreeRoot;
    
    /*
     * Given an Voronoi diagram in DCEL format:
     *  Extract all edges and map them to LineSegments
     *  Store with each segment the shepherd above it
     *  Incrementally add each segment to the vertical decomposition
     */
    public VerticalDecomposition(DCEL InGraph, MeshFilter m_meshFilter)
    {
        // *** Initialize vertical decomposition with one trapezoid containing the entire canvas ***
        // Find bounding box corners
        /* PREVIOUS
        float z = Vector2.Distance(m_meshFilter.transform.position, Camera.main.transform.position);
        var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, z));
        var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, z));
        LineSegment bottom = new LineSegment(new Vector2(bottomLeft.x, bottomLeft.z), new Vector2(topRight.x, bottomLeft.z), null);
        LineSegment top = new LineSegment(new Vector2(bottomLeft.x, topRight.z), new Vector2(topRight.x, topRight.z), null);
        // Create initial trapezoid and root of searchtree
        Trapezoid inital_trapezoid = new Trapezoid(new Vector2(bottomLeft.x, topRight.z), new Vector2(topRight.x, bottomLeft.z), top, bottom);*/
        float width = 10000;
        float height = 10000;
        LineSegment bottom = new LineSegment(new Vector2(-width, -height), new Vector2(width, -height), null);
        LineSegment top = new LineSegment(new Vector2(-width, height), new Vector2(width, height), null);
        // Create initial trapezoid and root of searchtree
        Trapezoid inital_trapezoid = new Trapezoid(new Vector2(-width, height), new Vector2(width, -height), top, bottom);
        traps.Add(inital_trapezoid);
         this.TreeRoot = new SearchNode(inital_trapezoid);
        inital_trapezoid.SetLeaf(TreeRoot);

        // *** Add all edges to the vertical decomposition ***
        // TODO Randomize the collection of half-edges
        ICollection<HalfEdge> edges = InGraph.Edges;
        Debug.Log("Adding voronoi with " + edges.Count + " halfedges");
        // Transform all DCEL edges into Linesegments
        foreach (HalfEdge edge in edges)
        {
            // Only add half-edges whose face is above it
            //  So only add half-edges whose to-vertex is to the right of the from-vertex
            //  So skip half-edges where 'to' has smaller x value than 'from'
            // Also skip edges going to and from the bounding box???
            if (edge.To.Pos.x > edge.From.Pos.x) { continue; }
            if (edge.To.Pos.x == edge.From.Pos.x && edge.To.Pos.y <= edge.From.Pos.y) { continue; }
            LineSegment segment = new LineSegment(edge.From.Pos, edge.To.Pos, edge.Face);
            Debug.Log("Inserting segment: " + segment.show());
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
        List<SearchNode> OldLeafs = new List<SearchNode>();
        List<SearchNode> NewLeafs = new List<SearchNode>();

        // Find all trapezoids intersecting the segment
        List<Trapezoid> Intersecting = this.FindIntersecting(seg);
        foreach (Trapezoid tr in Intersecting) traps.Remove(tr);

        // Find all leafs of intersecting trapezoids
        foreach (Trapezoid trap in Intersecting)
        {
            // Trapezoids should always point to leafs
            if (!trap.leaf.isLeaf) { throw new System.Exception(); }
            OldLeafs.Add(trap.leaf);
        }
        // Modify all intersecting trapezoids, by deleting, merging and creating trapezoids
        List<Trapezoid> newTrapezoids = this.CreateTrapezoids(Intersecting, seg);
        traps.AddRange(newTrapezoids);

        // Create new leafs for the new trapezoids
        foreach (Trapezoid trapezoid in newTrapezoids)
        {
            SearchNode leaf = new SearchNode(trapezoid);
            NewLeafs.Add(leaf);
            trapezoid.SetLeaf(leaf);
        }

        // Change old leafs and add inner nodes that point to new leafs
        this.UpdateSearchTree(OldLeafs, NewLeafs, seg);
    }

    /*
     * Given a point position, return the trapezoid that point is contained
     * 
     */
    public Trapezoid Search(Vector2 pos)
    {
        // Start the recursive search on the root of the searchtree
        Debug.Log(" --- Start search on position " + pos + " ---");
        return this.Search(pos, this.TreeRoot);
    }

    /*
     * Given a point position, return the trapezoid that point is contained
     * 
     * The linesegment is added for degenerate cases where two segments share an left endpoint
     *  This is used for finding the inital trapezoid that 
     */
    private Trapezoid Search(Vector2 pos, LineSegment l)
    {
        // Start the recursive search on the root of the searchtree
        //Debug.Log(" --- Start search on position " + pos + " ---");
        return this.Search(pos, this.TreeRoot, l);
    }

    // Recursive function to search the tree structure for the proper trapezoid
    private Trapezoid Search(Vector2 pos, SearchNode root, LineSegment l = null)
    {
        //root.show_children();
        if (root.isLeaf)
        {
            return root.leaf;
        }
        SearchNode node = root.Test(pos, l);
        return this.Search(pos, node, l);
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
        Trapezoid CurrentTrapezoid = this.Search(seg.point1, seg);
        Intersecting.Add(CurrentTrapezoid);
        while (seg.point2.x > CurrentTrapezoid.right.x && CurrentTrapezoid.Neighbors.Count > 0)
        {
            // Check neighbors to find if there are one or two neighbors, will return a list with one or two entries
            List<Trapezoid> RightNeighbors = CurrentTrapezoid.Neighbors.FindAll(
                delegate (Trapezoid trap)
                {
                    return (trap.left.x >= CurrentTrapezoid.right.x);
                });
            // If there is only one neighbor, it is the new trapezoid, else check which one is
            if (RightNeighbors.Count == 0)
            {
                Debug.LogWarning("Detected trapezoid that is not the right most trapezoid, but has no right neighbors:\n" + CurrentTrapezoid.show() + "\nWith segment: " + seg.show());
                string neighbors = "";
                foreach (Trapezoid n in CurrentTrapezoid.Neighbors)
                {
                    neighbors = neighbors + "\n" + n.show();
                }
                Debug.LogWarning(neighbors);
                break;
            }
            else if (RightNeighbors.Count == 1)
            {
                CurrentTrapezoid = RightNeighbors[0];
            }
            else if (RightNeighbors.Count > 1)
            {
                if (RightNeighbors.Count != 2) {
                    string neighbors = "";
                    foreach (Trapezoid n in CurrentTrapezoid.Neighbors)
                    {
                        neighbors = neighbors + "\n" + n.show();
                    }
                    Debug.LogError("A trapezoid cannot have more than 2 right neighbors, says it has: " + RightNeighbors.Count + "\n" + CurrentTrapezoid.show() + "\nAll Neighbors:" + neighbors);

                }
                // Report upper or lower, and find which one it is
                // If the right point of the current trapezoid is above the segment, then the next trapezoid is the lower neighbor, else the above neighbor
                if (seg.Above(CurrentTrapezoid.right))
                {
                    // If the left point of the bottom segment is equal to the right point of current, then it is upper neighbor. Except if there is only one neighbor
                    if (RightNeighbors[0].bottom.point1 == CurrentTrapezoid.right)
                    {
                        // Point is above the segment and second neighbor is lower neighbor
                        CurrentTrapezoid = RightNeighbors[1];
                    }
                    else
                    {
                        // Point is above segment and first neighbor is lower neighbor
                        CurrentTrapezoid = RightNeighbors[0];
                    }
                }
                else
                {
                    // If the left point of top segment is equal to the right point of current, then it is lower neighbor. Except if there is only one neighbor
                    if (RightNeighbors[0].top.point1 == CurrentTrapezoid.right)
                    {
                        // Point is below segment and second neighbor is upper neighbor
                        CurrentTrapezoid = RightNeighbors[1];
                    }
                    else
                    {
                        // Point is below segment and first neighbor is upper neighbor
                        CurrentTrapezoid = RightNeighbors[0];
                    }
                }
            }
            Intersecting.Add(CurrentTrapezoid);
            if (Intersecting.Count > 100)
            {
                Debug.LogError("Breaking out of suspected infinite loop at findIntersecting");
                break;
            }
        }
        string ts = "";
        foreach (Trapezoid t in Intersecting)
        {
            ts = ts + "\n" + t.show();
        }
        Debug.Log("Amount of found intersecting trapezoids: " + Intersecting.Count + ts);
        return Intersecting;
    }

    /*
     * Split all trapezoids that are being intersected by a segment 'seg'
     *  Replace top and bottom segments with the given segment
     *  Add new trapezoids at the start and end points (if those endpoints are not yet in the VD)
     *  Ensure all neighbors are correctly assigned
     *  
     *  Merge trapezoids where the vertical line has disappeard because of the segment
     */
    private List<Trapezoid> CreateTrapezoids(List<Trapezoid> toSplit, LineSegment seg)
    {
        List<Trapezoid> newTrapezoids = new List<Trapezoid>();
        List<Trapezoid> finalTrapezoids = new List<Trapezoid>();
        Trapezoid newUpper;
        Trapezoid newLower;
        Trapezoid oldUpper = null;
        Trapezoid oldLower = null;
        Trapezoid begin = null; // Used for a degenerate start
        bool StartDegenerate = false;
        bool EndDegenerate = false;

        /*
         * Simple case where the inserted segment is fully contained in one trapezoid
         * 
         * If the endpoints of the inserted segment touch another segment, use the regular case to avoid infinitesmall trapezoids
         */
        if (toSplit.Count == 1 && (toSplit[0].left != seg.point1 && toSplit[0].right != seg.point2))
        {
            Trapezoid start = new Trapezoid(toSplit[0].left, seg.point1, toSplit[0].top, toSplit[0].bottom);
            Trapezoid end = new Trapezoid(seg.point2, toSplit[0].right, toSplit[0].top, toSplit[0].bottom);
            foreach (Trapezoid neighbor in toSplit[0].Neighbors)
            {
                neighbor.DeleteNeighbor(toSplit[0]);
                // All left neighbors are neighbors of start, all right neighbors are neighbors of end
                if (neighbor.right.x > toSplit[0].left.x)
                {
                    neighbor.AddNeighbor(end);
                    end.AddNeighbor(neighbor);
                } else
                {
                    neighbor.AddNeighbor(start);
                    start.AddNeighbor(neighbor);
                }
            }
            Trapezoid above = new Trapezoid(seg.point1, seg.point2, toSplit[0].top, seg);
            Trapezoid below = new Trapezoid(seg.point1, seg.point2, seg, toSplit[0].bottom);
            above.AddNeighbor(start);
            above.AddNeighbor(end);
            below.AddNeighbor(start);
            below.AddNeighbor(end);
            start.AddNeighbor(above);
            start.AddNeighbor(below);
            end.AddNeighbor(above);
            end.AddNeighbor(below);
            // Add all trapezoids to output
            finalTrapezoids.Add(start);
            finalTrapezoids.Add(end);
            finalTrapezoids.Add(above);
            finalTrapezoids.Add(below);
            Debug.Log("Simple case, created trapezoids:\n" + start.show() + "\n" + end.show() + "\n" + above.show() + "\n" + below.show());
            return finalTrapezoids;
        }

        /*
         * Complex case where the inserted segment intersects multiple trapezoids
         */
        // If the left endpoint of the segment does not yet exist, create a trapezoid between that point and the left point of the first old trapezoid
        if (seg.point1.x != toSplit[0].left.x)
        {
            // This trapezoid will neighbor both next upper and lower neighbors
            begin = new Trapezoid(toSplit[0].left, seg.point1, toSplit[0].top, toSplit[0].bottom);
            foreach (Trapezoid neighbor in toSplit[0].Neighbors)
            {
                // Take the neighbors of the old trapezoid and add them to new trapezoid, except if the neighbor has been intersected by the segment
                neighbor.DeleteNeighbor(toSplit[0]);
                if (neighbor.left.x > begin.left.x) { continue; }
                neighbor.AddNeighbor(begin);
                begin.AddNeighbor(neighbor);
            }
            //oldLower = oldUpper; TODO Cleanup, this was most certainly a bug
            newTrapezoids.Add(begin);
            StartDegenerate = true;
        }
        // Split each intersected trapezoid in two, one trapezoid above the segment and one below (merging of trapezoids happens later)
        for (int i=0; i < toSplit.Count; i++)
        {
            // TODO, COMPLETELY WRONG, LEFT AND RIGHT POINTS ARE NOT DEFINED LIKE THIS. I think? Nope, I use this 'mistake' during the merging
            newUpper = new Trapezoid(toSplit[i].left, toSplit[i].right, toSplit[i].top, seg);
            newLower = new Trapezoid(toSplit[i].left, toSplit[i].right, seg, toSplit[i].bottom);
            Debug.Log("Considering trapezoid " + i + ". The oldLower is " + ((oldLower == null) ? "null" : oldLower.show()));
            /*
             * I clearly don't know what I am doing anymore, ignore this. TODO cleanup
            // The left point of the new trapezoids is on the segment, at the x-coord of the previous left point
            // The right point of the new trapezoids is on the segment, at the x-coord of the previous right point
            //Vector2 new_left = new Vector2(toSplit[i].left.x, seg.DetermineY(toSplit[i].left.x));
            //Vector2 new_right = new Vector2(toSplit[i].right.x, seg.DetermineY(toSplit[i].right.x));
            //newUpper = new Trapezoid(new_left, new_right, toSplit[i].top, seg);
            //newLower = new Trapezoid(new_left, new_right, seg, toSplit[i].bottom);
            */
            // If the first trapezoid is split in three, don't start the new trapezoids at the left point of trapezoid, but at segment
            if (StartDegenerate)
            {
                newUpper = new Trapezoid(seg.point1, toSplit[i].right, toSplit[i].top, seg);
                newLower = new Trapezoid(seg.point1, toSplit[i].right, seg, toSplit[i].bottom);
                begin.AddNeighbor(newUpper);
                begin.AddNeighbor(newLower);
                newUpper.AddNeighbor(begin);
                newLower.AddNeighbor(begin);
            }
            if (i == toSplit.Count - 1 && seg.point2.x != toSplit[toSplit.Count - 1].right.x)
            {
                // If the end is split in three, don't start new trapezoids at the right point, but at segment
                EndDegenerate = true;
                newUpper = new Trapezoid(toSplit[i].left, seg.point2, toSplit[i].top, seg);
                newLower = new Trapezoid(toSplit[i].left, seg.point2, seg, toSplit[i].bottom);
            }
            if (oldUpper != null)
            {
                newUpper.AddNeighbor(oldUpper);
                oldUpper.AddNeighbor(newUpper);
            }
            if (oldLower != null)
            {
                newLower.AddNeighbor(oldLower);
                oldLower.AddNeighbor(newLower);
            }
            // Every existing neighbor of the old trapezoid is either replaced (also intersected) or is a neighbor to the new top or lower
            foreach (Trapezoid neighbor in toSplit[i].Neighbors)
            {
                // Delete the trapezoid we are replacing from the neighbor list of all neighbors
                neighbor.DeleteNeighbor(toSplit[i]);
                // Don't add the neighbor if it has or will be replaced
                if (toSplit.Contains(neighbor)) { continue; }
                // Test if the neighbor belongs to upper or lower
                //  If the neighbor is left, its right point has to be above segment (to be upper neighbor)
                //  If the neighbor is right, its left point has to be above segment (to be upper neighbor)
                //      Neighbor is left if its right point is left of the right point of this trapezoid (neighboring trapezoids cannot have right points at same x)
                if (neighbor.right.x < toSplit[i].right.x)
                {
                    // Neighbor is left (Do not add any left neighbors on a degenerate start, as this is wrong)
                    if (StartDegenerate) { continue; }
                    // If the right point is on the segment, then there is a non degenerate start, and the neighbor could be the upper neighbor
                    //  Check if it is upper neighbor by checking if the right point of the neighbors bottom line on the segment
                    //   or the existing segment that contains seg.point2 is the top or bottom of the inital trapezoid
                    //      This rule is determined by schrift case 2
                    //  If it is lower neighbor, then seg.Above is also false and neighbor will be asigned in the else-clause
                    if (neighbor.right == seg.point1 && (neighbor.bottom.point2 == seg.point1 || toSplit[i].bottom.point1 == seg.point1))
                    {
                        newUpper.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newUpper);
                    }
                    else if (seg.Above(neighbor.right))
                    {
                        // Neighbor is upper neighbor
                        newUpper.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newUpper);
                    }
                    else
                    {
                        // Neighbor is lower neighbor
                        newLower.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newLower);
                    }
                }
                else
                {
                    // Neighbor is right (Do not add any right neighbors on a degenerate end, as the degenerate trapezoid will still be placed)
                    if (EndDegenerate) { continue; }
                    // If the left point is on the segment, then there is a non degenerate end, and the neighbor could be the upper neighbor
                    //  Check if it is upper neighbor by checking if the left point of the neighbors bottom line is on the segment
                    //   or the existing segment that contains seg.point2 is the top or bottom of the inital trapezoid
                    //      This rule is determined by schrift case 2
                    //  If it is lower neighbor, then seg.Above is also false and neighbor will be asigned in the else-clause
                    if (neighbor.left == seg.point2 && (neighbor.bottom.point1 == seg.point2 || toSplit[i].bottom.point2 == seg.point2))
                    {
                        newUpper.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newUpper);
                    }
                    else if (seg.Above(neighbor.left))
                    {
                        // Neighbor is upper neighbor
                        newUpper.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newUpper);
                    }
                    else
                    {
                        // Neighbor is lower neighbor
                        newLower.AddNeighbor(neighbor);
                        neighbor.AddNeighbor(newLower);
                    }
                }
            }
            Debug.Log("CreatingTraps:\n" + newUpper.show() + " AND\n" + newLower.show());
            newTrapezoids.Add(newUpper);
            newTrapezoids.Add(newLower);
            // Point the old trapezoid to the replacements
            toSplit[i].LowerReplacement = newLower;
            toSplit[i].UpperReplacement = newUpper;
            // Set new trapezoids to old for next iteration
            oldUpper = newUpper;
            oldLower = newLower;
            // After one iteration we have passed the start
            StartDegenerate = false;
        }
        // If the right endpoint of the segment does not yet exist, create a trapezoid between that point and the right point of the last old trapezoid
        if (seg.point2.x != toSplit[toSplit.Count - 1].right.x)
        {
            // This trapezoid will neigbor both previous lower and upper trapezoids (It's called newupper because that name is available now)
            newUpper = new Trapezoid(seg.point2, toSplit[toSplit.Count - 1].right, toSplit[toSplit.Count - 1].top, toSplit[toSplit.Count - 1].bottom);
            newUpper.AddNeighbor(oldUpper);
            oldUpper.AddNeighbor(newUpper);
            newUpper.AddNeighbor(oldLower);
            oldLower.AddNeighbor(newUpper);
            // This trapezoid will also neighbor all right neighbors of the old trapezoid
            foreach (Trapezoid neighbor in toSplit[toSplit.Count - 1].Neighbors)
            {
                // Take the neighbors of the old trapezoid and add them to new trapezoid, except if the neighbor has been intersected by the segment
                neighbor.DeleteNeighbor(toSplit[toSplit.Count - 1]);
                if (neighbor.right.x < newUpper.right.x) { continue; }
                neighbor.AddNeighbor(newUpper);
                newUpper.AddNeighbor(neighbor);
            }
            newTrapezoids.Add(newUpper);
            Debug.Log("Created degenerate end trapezoid: " + newUpper.show());
        }

        /* Merge trapezoids
         * If the trapezoid has the segment as top, but the right point is above the segment, this trapezoid should be merged with its neighbor
         * If the trapezoid has the segment as bottom, but the right point is below the segment, this trapezoid should be merged with its neighbor
         *  Note, if the right point is on the segment, we have reached the end of the segment and nothing should be done
         */
        for (int i = 0; i < newTrapezoids.Count; i++)
        {
            if (newTrapezoids[i].top == seg)
            {
                // Trapezoid is lower trapezoid
                if (seg.Above(newTrapezoids[i].right)) // Will only be true if point is above segment
                {
                    // Find neighbor and merge
                    // Note that there can only be 1 neighbor to the right of this trapezoid
                    bool foundNeighbor = false;
                    foreach (Trapezoid t in newTrapezoids[i].Neighbors)
                    {
                        if (t.right.x >= newTrapezoids[i].right.x)
                        {
                            foundNeighbor = true;
                            // Merge the two trapezoids by modifying 't' and replacing all neighbors with 't'
                            //  Note: newTrapezoids[i].top = t.top = seg and newTrapezoids[i].bottom = t.bottom
                            //  Note: t.right should stay t.right, thus only t.left has to be updated
                            t.left = newTrapezoids[i].left;
                            // Add all neighbors of newTrapezoids[i] to t, also update the neighbors
                            foreach (Trapezoid neighbor in newTrapezoids[i].Neighbors)
                            {
                                neighbor.DeleteNeighbor(newTrapezoids[i]);
                                if (neighbor.Equals(t)) { continue; }
                                neighbor.AddNeighbor(t);
                                t.AddNeighbor(neighbor);
                            }
                            // Change replacement pointer from newTrapezoid[i] to t
                            newTrapezoids[i].LowerReplacement = t;
                            // Note: t will be tested in a next iteration to see if it has to be merged again or if it can be reported
                        }
                    }
                    if (!foundNeighbor)
                    {
                        Debug.LogError("A lower trapezoid should be merged but no suitable neighbor can be found\n" + newTrapezoids[i].show());
                        string neighbors = "";
                        foreach (Trapezoid n in newTrapezoids[i].Neighbors)
                        {
                            neighbors = neighbors + "\n" + n.show();
                        }
                        Debug.LogError("Neighbors: " + neighbors);
                    }
                }
                else
                {
                    // There is no problem, just add the trapezoid to the output
                    finalTrapezoids.Add(newTrapezoids[i]);
                }
            }
            else if (newTrapezoids[i].bottom == seg)
            {
                // Trapezoid is upper trapezoid
                if (!seg.Above(newTrapezoids[i].right) && (seg.point2 != newTrapezoids[i].right)) // Wil also be true if point is on segment, test for that too
                {
                    // Find neighbor and merge
                    // Note that there can only be 1 neighbor to the right of this trapezoid
                    bool foundNeighbor = false;
                    foreach (Trapezoid t in newTrapezoids[i].Neighbors)
                    {
                        if (t.right.x >= newTrapezoids[i].right.x)
                        {
                            foundNeighbor = true;
                            // Merge the two trapezoids by modifying 't' and replacing all neighbors with 't'
                            //  Note: newTrapezoids[i].top = t.top and newTrapezoids[i].bottom = t.bottom = seg
                            //  Note: t.right should stay t.right, thus only t.left has to be updated
                            t.left = newTrapezoids[i].left;
                            // Add all neighbors of newTrapezoids[i] to t, also update the neighbors
                            foreach (Trapezoid neighbor in newTrapezoids[i].Neighbors)
                            {
                                neighbor.DeleteNeighbor(newTrapezoids[i]);
                                if (neighbor.Equals(t)) { continue; }
                                neighbor.AddNeighbor(t);
                                t.AddNeighbor(neighbor);
                            }
                            // Change replacement pointer from newTrapezoid[i] to t
                            newTrapezoids[i].UpperReplacement = t;
                            // Note: t will be tested in a next iteration to see if it has to be merged again or if it can be reported
                        }
                    }
                    if (!foundNeighbor)
                    {
                        Debug.LogError("An upper trapezoid should be merged but no suitable neighbor can be found\n" + newTrapezoids[i].show());
                        string neighbors = "";
                        foreach (Trapezoid n in newTrapezoids[i].Neighbors)
                        {
                            neighbors = neighbors + "\n" + n.show();
                        }
                        Debug.LogError("Neighbors: " + neighbors);
                    }
                }
                else
                {
                    // No problem, add trapezoid to output
                    finalTrapezoids.Add(newTrapezoids[i]);
                }
            }
            else
            {
                // These are the outer trapezoids next to the segment, these will never have to be merged, so add them to the output
                finalTrapezoids.Add(newTrapezoids[i]);
            }
        }
        return finalTrapezoids;
    }

    /*
     * Modify the search tree by changing old leafs and adding new leafs and internal nodes
     * 
     * Handle the first and last node separately, because they might have created 3 new trapezoids
     */
     private void UpdateSearchTree(List<SearchNode> OldLeafs, List<SearchNode> NewLeafs, LineSegment segment)
    {
        // *** Case when segment intersects only one trapezoid
        // This case is only special when it does not touch either side of the trapezoid but is fully contained
        //  If it is not special, only 2 or 3 trapezoids are created which can be done with the regular code
        if (OldLeafs.Count == 1 && NewLeafs.Count == 4)
        {
            // Replace leaf with an x-node
            // That x-node has a leaf and x-node as left and right child
            // The second x-node has a y-node and leaf as left and right child
            // The y-node has two leafs
            SearchNode x2node = new SearchNode(segment.point2);
            SearchNode ynode = new SearchNode(segment);
            OldLeafs[0].setChildren(NewLeafs[0], x2node, "Special case UST");
            x2node.setChildren(ynode, NewLeafs[1], "Special case UST");
            ynode.setChildren(NewLeafs[3], NewLeafs[2], "Special case UST");
            OldLeafs[0].update(segment.point1);
            return;
        }
        // *** Case when segment intersects many trapezoids ***
        // If there are trapezoids to the left or right of the segment, then those old trapezoids have special treatment and are not included in the loop
        int start = 0;
        int end = 0;
        // Check if the first old trapezoid contains the left segment point or if that point equals the left point of that trapezoid
        if (OldLeafs[0].leaf.left.x != segment.point1.x)
        {
            // replace the leaf with an x-node for the left endpoint of segment and a y-node for the segment
            //  The first leaf in NewLeafs will be the trapezoid to the left of the segment (thus left child of previous leaf)
            SearchNode node = new SearchNode(segment);
            OldLeafs[0].setChildren(NewLeafs[0], node, "First trapezoid UST 1");
            node.setChildren(this.FindLowerReplacement(OldLeafs[0].leaf).leaf, // lower leaf
                this.FindUpperReplacement(OldLeafs[0].leaf).leaf, "First trapezoid UST 2"); // upper leaf
            OldLeafs[0].update(segment.point1);
            // Skip the first iteration of the replacement loop since the leaf of the first trapezoid has already been updated
            start = 1;
        }
        // Check if the last old trapezoid contains the right segment point or if that point equals the right point of that trapezoid
        //  In case when only 1 trap is intersected, AND there are only 3 trapezoids created, AND there is space between segment and left wall of trapezoid
        //  then Oldleafs[0] == Oldleafs[count-1] and oldleafs[0].leaf has already been changed, so this check does not need to happen
        Debug.Log("Updating " + OldLeafs.Count + " searchtree leafs, last leaf:\n" + OldLeafs[OldLeafs.Count - 1].show());
        if (OldLeafs[OldLeafs.Count-1].isLeaf && OldLeafs[OldLeafs.Count-1].leaf.right.x != segment.point2.x)
        {
            // replace the leaf with an x-node for the left endpoint of segment and a y-node for the segment
            //  The last leaf in NewLeafs will be the trapezoid to the right of the segment (thus right child of previous leaf)
            SearchNode node = new SearchNode(segment);
            OldLeafs[OldLeafs.Count - 1].setChildren(node, NewLeafs[NewLeafs.Count - 1], "Last trapezoid UST 1");
            node.setChildren(this.FindLowerReplacement(OldLeafs[OldLeafs.Count - 1].leaf).leaf, // lower leaf
                this.FindUpperReplacement(OldLeafs[OldLeafs.Count - 1].leaf).leaf, // upper leaf
                "Last trapezoid UST 2");
            OldLeafs[OldLeafs.Count - 1].update(segment.point2);
            // Skip the last iteration of the replacement loop since the leaf of the last trapezoid has alreadt been updated
            end = 1;
        }


        // Each other leaf is replaced by Y-nodes with the segment, with the lower and upper leafs as childs
        for (int i = start; i < OldLeafs.Count - end; i++)
        {
            OldLeafs[i].setChildren(this.FindLowerReplacement(OldLeafs[i].leaf).leaf, this.FindUpperReplacement(OldLeafs[i].leaf).leaf, "General trapezoid UST");
            OldLeafs[i].update(segment);
        }
    }

    /*
     * Find lower replacement
     */
    public Trapezoid FindLowerReplacement(Trapezoid trap, int i = 1)
    {
        if (trap.LowerReplacement == null) {
            Debug.Log("Replacing trapezoid (step = " + i + ") with: " + trap.show() + "\nHas replacement: " + trap.LowerReplacement);
            return trap; }
        return this.FindLowerReplacement(trap.LowerReplacement, i+1);
    }

    /*
     * Find upper replacement
     */
    public Trapezoid FindUpperReplacement(Trapezoid trap)
    {
        if (trap.UpperReplacement == null) { return trap; }
        return this.FindUpperReplacement(trap.UpperReplacement);
    }
}

public class Trapezoid
{
    public LineSegment top;
    public LineSegment bottom;
    public Vector2 left;
    public Vector2 right;
    public List<Trapezoid> Neighbors = new List<Trapezoid>();
    public SearchNode leaf;
    // Once a trapezoid is set to be replaced, store pointers to the trapezoids it will be replaced by here
    public Trapezoid LowerReplacement;
    public Trapezoid UpperReplacement;

    /* 
     * Create a trapezoid represented by two linesegments above and below, and two points left and right
     */
    public Trapezoid(Vector2 l, Vector2 r, LineSegment t, LineSegment b)
    {
        top = t;
        bottom = b;
        left = l;
        right = r;

        // cleanup eventually
        if (left.x > right.x)
        {
            Debug.LogError("Created trapezoid with wrong points\n" + this.show());
        }
        if (top.Above(bottom.point1) || top.Above(bottom.point2))
        {
            Debug.LogError("Created trapezoid with bottom segment point above the top segment\n" + this.show());
        }
    }

    public bool Equals(Trapezoid t)
    {
        //Check for null and compare run-time types.
        if ((t == null) || !this.GetType().Equals(t.GetType()))
        {
            return false;
        }
        else
        {
            return (top == t.top) && (bottom == t.bottom)
                && (left == t.left) && (right == t.right);
        }
    }

    /*
     * Give the trapezoid a pointer to the leaf that contains it
     */
    public void SetLeaf(SearchNode leafNode)
    {
        this.leaf = leafNode;
    }

    /*
     * Add a new neighbor to the trapezoid
     */
     public void AddNeighbor(Trapezoid neighbor)
    {
        if (neighbor == null) { return; }

        // Desperate attempt at stopping weird neighbor behavoir
        // Only add valid neighbors
        if ((neighbor.left.x == this.right.x || neighbor.right.x == this.left.x)
            && !this.Neighbors.Contains(neighbor))
        {
            this.Neighbors.Add(neighbor);
        }
        else
        {
            Debug.LogWarning("Weird neighbor behavior spotted for " + this.show() + "\nWith " + neighbor.show());
        }
    }

    /*
     * Delete neighbor from the trapezoid
     */
    public void DeleteNeighbor(Trapezoid neighbor)
    {
        if (neighbor == null) { return; }
        this.Neighbors.Remove(neighbor);
    }

    /*
     * Print the trapezoid
     */
     public string show()
    {
        return ("Trapezoid (Neighbors = " + this.Neighbors.Count + ") with segments: " + this.top.show() + " and " + this.bottom.show()
            + ". Endpoints: " + "(" + this.left.x + ", " + this.left.y + ") and (" + this.right.x + ", " + this.right.y + ")");
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
     *  Includes an epsilon to allow floating point inaccuracies
     * Return false is the point is not directly below, above or on the segment
     * 
     * Find left point of segment
     * Determine slope of segment                           (slope = diffY / diffX)
     * Interpolate y of segment at x pos of given point     (below = pos.y < (pos.x - smallest x) * slope + y of smallest x)
     * Test if the y of the point is above
     */
    public bool Above(Vector2 point)
    {
        double eps = 1e-3;
        if (this.point1.x > point.x || this.point2.x < point.x) { return false; }
        double slope = (this.point1.y - this.point2.y) / (this.point1.x - this.point2.x);
        return (point.y > this.point1.y + (point.x - this.point1.x) * slope + eps);
    }

    /*
     * Determine the y-value of a point on the segment given an x-value (even when segment does not contain this x-value)
     */
    public float DetermineY(float x)
    {
        float slope = (this.point1.y - this.point2.y) / (this.point1.x - this.point2.x);
        return this.point1.y + (x - this.point1.x) * slope;
    }

    // Print the segment points
    public string show()
    {
        return "(" + this.point1.x + ", " + this.point1.y + "), (" + this.point2.x + ", " + this.point2.y + ")";
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
     * Constructor for leaf nodes
     */
    public SearchNode(Trapezoid trap)
    {
        this.leaf = trap;
        this.isLeaf = true;
    }

    /*
     * Constructor for X-nodes
     */
    public SearchNode(Vector2 endpoint)
    {
        this.isLeaf = false;
        this.isXNode = true;
        this.storeX = endpoint;
    }

    /*
     * Constructor for Y-nodes
     */
    public SearchNode(LineSegment segment)
    {
        this.isLeaf = false;
        this.isXNode = false;
        this.storeY = segment;
    }

    /*
     * Update node to X-node
     */
    public void update(Vector2 endpoint)
    {
        isLeaf = false;
        leaf = null;
        isXNode = true;
        storeX = endpoint;
    }

    /*
     * Update node to Y-node
     */
    public void update(LineSegment segment)
    {
        isLeaf = false;
        leaf = null;
        isXNode = false;
        storeY = segment;
    }

    /*
     * Set the childern of searchnodes
     */
     public void setChildren(SearchNode left, SearchNode right, string source)
    {
        if (left == null || right == null)
        {
            Debug.LogError("Leaf was updated without proper children at '" + source + "': "
                + this.show() + ". Children: "
                + ((left != null) ? left.show() : "null") + " --- "
                + ((right != null) ? right.show() : "null"));
        }
        this.leftChild = left;
        this.rightChild = right;
    }


    /*
     * Return the left or right child of the node depending a given position and the stored component
     */
    public SearchNode Test(Vector2 pos, LineSegment segment)
    {
        //Debug.Log("Test for position: " + pos);
        //Debug.Log(this.show());
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
            /* If point is below the segment, return left
             * If point is above the segment, return right
             * If the point is on the segment, return right, or (when segment specified), determine slope and return left if slope is smaller
             *  slope = diffY / diffX
             *  below == pos.y < (pos.x - smallest x) * slope + y of smallest x
             */
            double slope = (storeY.point1.y - storeY.point2.y) / (storeY.point1.x - storeY.point2.x);
            // *** Special case when finding inital trapezoid ***
            // Book: Similarly, whenever p lies on a segment s of a y - node(this can only happen if si shares its left endpoint, p, with s)
            if (segment != null && segment.point1 == this.storeY.point1)
            {
                // The point is on top of a segment, if the slope of the new segment is larger, we decide that p lies above the stored segment, otherwise we decide that it is below s.
                // Used when searching for the inital trapezoid for the inserting of a new segment
                if (slope < (segment.point1.y - segment.point2.y) / (segment.point1.x - segment.point2.x))
                {
                    return rightChild;
                }
                else
                {
                    return leftChild;
                }
            }
            // *** Regular case ***
            if (pos.y < storeY.point1.y + (pos.x - storeY.point1.x) * slope)
            {
                return leftChild;
            } else
            {
                return rightChild;
            }
        }
    }
    
    public string ToString()
    {
        return this.show();
    }

    public string show()
    {
        if (this.isLeaf)
        {
            return "Leafnode with " + this.leaf.show();
        }
        else if (this.isXNode)
        {
            return "Xnode with endpoint: (" + this.storeX.x + ", " + this.storeX.y + ")";
        }
        else
        {
            return "Ynode with segment: " + this.storeY.show();
        }
    }

    public void show_children()
    {
        Debug.Log(this.show());
        string left = "null";
        string right = "null";
        if (this.leftChild != null)
        {
            left = this.leftChild.show();
        }
        if (this.rightChild != null)
        {
            right = this.rightChild.show();
        }
        Debug.Log(left + " --- " + right);
    }
}
