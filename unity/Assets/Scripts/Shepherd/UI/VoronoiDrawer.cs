namespace Shepherd
{
    using UnityEngine;
    using Util.Geometry.Triangulation;

    /// <summary>
    /// Static class responsible for displaying voronoi graph and concepts.
    /// Draws the Voronoi graph, as well as edges of Delaunay triangulation and circumcircles of delaunay triangles.
    /// </summary>
    public static class VoronoiDrawer
    {
        // toggle variables for displaying circles, edges, and voronoi graph
        public static bool CircleOn { get; set; }
        public static bool EdgesOn { get; set; }
        public static bool VoronoiOn { get; set; }

        // line material for Unity shader
        private static Material m_lineMaterial;

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

        /// <summary>
        /// Draws the voronoi diagram related to delaunay triangulation
        /// </summary>
        /// <param name="m_Delaunay"></param>
        private static void DrawVoronoi(Triangulation m_Delaunay)
        {
            GL.Begin(GL.LINES);
            GL.Color(Color.black);

            foreach (var halfEdge in m_Delaunay.Edges)
            {
                // do not draw edges for outer triangles
                if (m_Delaunay.ContainsInitialPoint(halfEdge.T))
                {
                    continue;
                }

                // find relevant triangles to triangle edge
                Triangle t1 = halfEdge.T;
                Triangle t2 = halfEdge.Twin.T;

                if (t1 != null && !t1.Degenerate &&
                    t2 != null && !t2.Degenerate)
                {
                    // draw edge between circumcenters
                    var v1 = t1.Circumcenter.Value;
                    var v2 = t2.Circumcenter.Value;
                    GL.Vertex3(v1.x, v1.y, 0);
                    GL.Vertex3(v2.x, v2.y, 0);
                }
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
            if (VoronoiOn) DrawVoronoi(m_Delaunay);
        }
    }
}
