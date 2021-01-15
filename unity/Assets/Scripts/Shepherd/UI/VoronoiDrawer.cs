namespace Shepherd
{
    using UnityEngine;
    using Util.Geometry.Triangulation;
    using Util.Geometry.DCEL;

    /// <summary>
    /// Static class responsible for displaying voronoi graph and concepts.
    /// Draws the Voronoi graph, as well as edges of Delaunay triangulation and circumcircles of delaunay triangles.
    /// Edited from Voronoi implementation, also draws vertical decomposition
    /// </summary>
    public static class VoronoiDrawer
    {
        // toggle variables for displaying circles, edges, and voronoi graph
        public static bool CircleOn { get; set; }
        public static bool EdgesOn { get; set; }
        public static bool VoronoiOn { get; set; }

        // line material for Unity shader
        private static Material m_lineMaterial;

        private static VerticalDecomposition verticalDecomposition;
        private static DCEL voronoi_dcel;


        public static void CreateLineMaterial()
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            m_lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            // Turn on alpha blending
            m_lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // Turn backface culling off
            m_lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            // Turn off depth writes
            m_lineMaterial.SetInt("_ZWrite", 0);
        }

        /// <summary>
        /// Draw edges of the Delaunay triangulation
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawEdges(Triangulation m_Delaunay)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.green);

            foreach (var halfEdge in m_Delaunay.Edges)
            {
                // dont draw edges to outer vertices
                if (m_Delaunay.ContainsInitialPoint(halfEdge.T))
                {
                    continue;
                }

                // draw edge
                GL.Vertex3(halfEdge.Point1.x, halfEdge.Point1.y, 0);
                GL.Vertex3(halfEdge.Point2.x, halfEdge.Point2.y, 0);
            }

            GL.End();
        }

        /// <summary>
        /// Draws the circumcircles of the Delaunay triangles
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawCircles(Triangulation m_Delaunay)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.blue);

            //const float extra = (360 / 100);

            foreach (Triangle triangle in m_Delaunay.Triangles)
            {
                // dont draw circles for triangles to outer vertices
                if (m_Delaunay.ContainsInitialPoint(triangle) || triangle.Degenerate)
                {
                    continue;
                }

                var center = triangle.Circumcenter.Value;

                // find circle radius
                var radius = Vector2.Distance(center, triangle.P0);

                var prevA = 0f;
                for (var a = 0f; a <= 2 * Mathf.PI; a += 0.05f)
                {
                    //the circle.
                    GL.Vertex3(Mathf.Cos(prevA) * radius + center.x, Mathf.Sin(prevA) * radius + center.y, 0);
                    //Debug.Log(Mathf.Cos(prevA) * radius + center.x, Mathf.Sin(prevA) * radius + center.y, 0);
                    GL.Vertex3(Mathf.Cos(a) * radius + center.x, Mathf.Sin(a) * radius + center.y, 0);

                    //midpoint of the circle.
                    GL.Vertex3(Mathf.Cos(prevA) * 0.1f + center.x, Mathf.Sin(prevA) * 0.1f + center.y, 0);
                    GL.Vertex3(Mathf.Cos(a) * 0.1f + center.x, Mathf.Sin(a) * 0.1f + center.y, 0);

                    prevA = a;
                }
            }

            GL.End();
        }
        public static void setDCEL(DCEL dcel)
        {
            voronoi_dcel = dcel;
        }

        private static void drawDCEL(DCEL dcel)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.black);

            foreach (HalfEdge halfEdge in dcel.Edges)
            {
                GL.Vertex3(halfEdge.From.Pos.x, halfEdge.From.Pos.y, 0);
                GL.Vertex3(halfEdge.To.Pos.x, halfEdge.To.Pos.y, 0);
            }
            GL.End();
        }

        private static Vector2 intersect(LineSegment ls, Vector2 p)
        {
            var x1 = ls.point1.x;
            var x2 = ls.point2.x;
            var y1 = ls.point1.y;
            var y2 = ls.point2.y;

            var xf = p.x;

            var yf = y1 + ((xf - x1) * (y2 - y1)) / (x2 - x1);

            return new Vector2(xf, yf);
        }
        public static void SetVD(VerticalDecomposition vd)
        {
            verticalDecomposition = vd;
        }
        public static void DrawVD(VerticalDecomposition vd)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.cyan);

            foreach (Trapezoid t in vd.traps)
            {
                // only need to draw left bounds since very rightmost is always out of frame
                var l1 = intersect(t.top, t.left);
                var l2 = intersect(t.bottom, t.left);
                GL.Vertex3(l1.x, l1.y, 0);
                GL.Vertex3(l2.x, l2.y, 0);
            }
            GL.End();
        }

        /// <summary>
        /// Main drawing function that calls other auxiliary functions.
        /// </summary>
        /// <param name="m_Delaunay"></param>
        public static void Draw(Triangulation m_Delaunay)
        {
            m_lineMaterial.SetPass(0);

            // call functions that are set to true
            if (EdgesOn) DrawEdges(m_Delaunay);
            if (CircleOn) DrawCircles(m_Delaunay);
            if (VoronoiOn)
            {
                drawDCEL(voronoi_dcel);
                if (verticalDecomposition != null)
                {
                    DrawVD(verticalDecomposition);
                }
            }
        }

        /// <summary>
        /// Draws vertical decomposition
        /// </summary>
        /// <param name="vd"></param>
        


    }
}
