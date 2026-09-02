using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// Procedural mesh construction for the operations core. Everything the machine is made of
/// is generated here: there are no imported models, so every form is authored to mean
/// something rather than to fill space.
/// </summary>
public static class MeshFactory
{
    public sealed class Builder
    {
        public Point3DCollection Positions { get; } = new();
        public Vector3DCollection Normals { get; } = new();
        public Int32Collection Indices { get; } = new();

        public int Count => Positions.Count;

        public void Quad(Point3D a, Point3D b, Point3D c, Point3D d)
        {
            var normal = Normal(a, b, c);
            int i = Positions.Count;
            Positions.Add(a); Positions.Add(b); Positions.Add(c); Positions.Add(d);
            for (int k = 0; k < 4; k++) Normals.Add(normal);
            Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
            Indices.Add(i); Indices.Add(i + 2); Indices.Add(i + 3);
        }

        public void Tri(Point3D a, Point3D b, Point3D c)
        {
            var normal = Normal(a, b, c);
            int i = Positions.Count;
            Positions.Add(a); Positions.Add(b); Positions.Add(c);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
        }

        /// <summary>Quad with explicit vertex normals, used where a surface must read as curved.</summary>
        public void QuadSmooth(Point3D a, Vector3D na, Point3D b, Vector3D nb, Point3D c, Vector3D nc, Point3D d, Vector3D nd)
        {
            int i = Positions.Count;
            Positions.Add(a); Positions.Add(b); Positions.Add(c); Positions.Add(d);
            Normals.Add(na); Normals.Add(nb); Normals.Add(nc); Normals.Add(nd);
            Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
            Indices.Add(i); Indices.Add(i + 2); Indices.Add(i + 3);
        }

        public MeshGeometry3D ToMesh(bool freeze = true)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = Positions,
                Normals = Normals,
                TriangleIndices = Indices
            };
            if (freeze) mesh.Freeze();
            return mesh;
        }

        private static Vector3D Normal(Point3D a, Point3D b, Point3D c)
        {
            var n = Vector3D.CrossProduct(b - a, c - a);
            if (n.LengthSquared < 1e-12) return new Vector3D(0, 1, 0);
            n.Normalize();
            return n;
        }
    }

    private static Point3D P(double x, double y, double z) => new(x, y, z);

    // ---------------------------------------------------------------- Box

    /// <summary>Axis-aligned box with an optional chamfer, which is what stops parts reading as cuboids.</summary>
    public static void Box(Builder b, Point3D centre, double sx, double sy, double sz, double chamfer = 0)
    {
        double hx = sx / 2, hy = sy / 2, hz = sz / 2;
        double c = Math.Min(chamfer, Math.Min(hx, Math.Min(hy, hz)) * 0.7);
        double cx = centre.X, cy = centre.Y, cz = centre.Z;

        if (c <= 0.0005)
        {
            Point3D[] v =
            {
                P(cx-hx,cy-hy,cz-hz), P(cx+hx,cy-hy,cz-hz), P(cx+hx,cy+hy,cz-hz), P(cx-hx,cy+hy,cz-hz),
                P(cx-hx,cy-hy,cz+hz), P(cx+hx,cy-hy,cz+hz), P(cx+hx,cy+hy,cz+hz), P(cx-hx,cy+hy,cz+hz)
            };
            b.Quad(v[4], v[5], v[6], v[7]);   // +Z
            b.Quad(v[1], v[0], v[3], v[2]);   // -Z
            b.Quad(v[5], v[1], v[2], v[6]);   // +X
            b.Quad(v[0], v[4], v[7], v[3]);   // -X
            b.Quad(v[3], v[7], v[6], v[2]);   // +Y
            b.Quad(v[0], v[1], v[5], v[4]);   // -Y
            return;
        }

        // Chamfered box: six inset faces plus twelve bevel strips and eight corner triangles.
        double ix = hx - c, iy = hy - c, iz = hz - c;

        // Faces (inset on their two in-plane axes).
        b.Quad(P(cx-ix,cy-iy,cz+hz), P(cx+ix,cy-iy,cz+hz), P(cx+ix,cy+iy,cz+hz), P(cx-ix,cy+iy,cz+hz));
        b.Quad(P(cx+ix,cy-iy,cz-hz), P(cx-ix,cy-iy,cz-hz), P(cx-ix,cy+iy,cz-hz), P(cx+ix,cy+iy,cz-hz));
        b.Quad(P(cx+hx,cy-iy,cz+iz), P(cx+hx,cy-iy,cz-iz), P(cx+hx,cy+iy,cz-iz), P(cx+hx,cy+iy,cz+iz));
        b.Quad(P(cx-hx,cy-iy,cz-iz), P(cx-hx,cy-iy,cz+iz), P(cx-hx,cy+iy,cz+iz), P(cx-hx,cy+iy,cz-iz));
        b.Quad(P(cx-ix,cy+hy,cz+iz), P(cx+ix,cy+hy,cz+iz), P(cx+ix,cy+hy,cz-iz), P(cx-ix,cy+hy,cz-iz));
        b.Quad(P(cx-ix,cy-hy,cz-iz), P(cx+ix,cy-hy,cz-iz), P(cx+ix,cy-hy,cz+iz), P(cx-ix,cy-hy,cz+iz));

        // Edge bevels along X.
        b.Quad(P(cx-ix,cy+iy,cz+hz), P(cx+ix,cy+iy,cz+hz), P(cx+ix,cy+hy,cz+iz), P(cx-ix,cy+hy,cz+iz));
        b.Quad(P(cx-ix,cy-hy,cz+iz), P(cx+ix,cy-hy,cz+iz), P(cx+ix,cy-iy,cz+hz), P(cx-ix,cy-iy,cz+hz));
        b.Quad(P(cx+ix,cy+iy,cz-hz), P(cx-ix,cy+iy,cz-hz), P(cx-ix,cy+hy,cz-iz), P(cx+ix,cy+hy,cz-iz));
        b.Quad(P(cx+ix,cy-hy,cz-iz), P(cx-ix,cy-hy,cz-iz), P(cx-ix,cy-iy,cz-hz), P(cx+ix,cy-iy,cz-hz));
        // Edge bevels along Z.
        b.Quad(P(cx+hx,cy+iy,cz+iz), P(cx+hx,cy+iy,cz-iz), P(cx+ix,cy+hy,cz-iz), P(cx+ix,cy+hy,cz+iz));
        b.Quad(P(cx+ix,cy-hy,cz+iz), P(cx+ix,cy-hy,cz-iz), P(cx+hx,cy-iy,cz-iz), P(cx+hx,cy-iy,cz+iz));
        b.Quad(P(cx-hx,cy+iy,cz-iz), P(cx-hx,cy+iy,cz+iz), P(cx-ix,cy+hy,cz+iz), P(cx-ix,cy+hy,cz-iz));
        b.Quad(P(cx-ix,cy-hy,cz-iz), P(cx-ix,cy-hy,cz+iz), P(cx-hx,cy-iy,cz+iz), P(cx-hx,cy-iy,cz-iz));
        // Edge bevels along Y.
        b.Quad(P(cx+ix,cy-iy,cz+hz), P(cx+hx,cy-iy,cz+iz), P(cx+hx,cy+iy,cz+iz), P(cx+ix,cy+iy,cz+hz));
        b.Quad(P(cx-hx,cy-iy,cz+iz), P(cx-ix,cy-iy,cz+hz), P(cx-ix,cy+iy,cz+hz), P(cx-hx,cy+iy,cz+iz));
        b.Quad(P(cx+hx,cy-iy,cz-iz), P(cx+ix,cy-iy,cz-hz), P(cx+ix,cy+iy,cz-hz), P(cx+hx,cy+iy,cz-iz));
        b.Quad(P(cx-ix,cy-iy,cz-hz), P(cx-hx,cy-iy,cz-iz), P(cx-hx,cy+iy,cz-iz), P(cx-ix,cy+iy,cz-hz));

        // Corner facets.
        b.Tri(P(cx+ix,cy+iy,cz+hz), P(cx+hx,cy+iy,cz+iz), P(cx+ix,cy+hy,cz+iz));
        b.Tri(P(cx-hx,cy+iy,cz+iz), P(cx-ix,cy+iy,cz+hz), P(cx-ix,cy+hy,cz+iz));
        b.Tri(P(cx+hx,cy+iy,cz-iz), P(cx+ix,cy+iy,cz-hz), P(cx+ix,cy+hy,cz-iz));
        b.Tri(P(cx-ix,cy+iy,cz-hz), P(cx-hx,cy+iy,cz-iz), P(cx-ix,cy+hy,cz-iz));
        b.Tri(P(cx+hx,cy-iy,cz+iz), P(cx+ix,cy-iy,cz+hz), P(cx+ix,cy-hy,cz+iz));
        b.Tri(P(cx-ix,cy-iy,cz+hz), P(cx-hx,cy-iy,cz+iz), P(cx-ix,cy-hy,cz+iz));
        b.Tri(P(cx+ix,cy-iy,cz-hz), P(cx+hx,cy-iy,cz-iz), P(cx+ix,cy-hy,cz-iz));
        b.Tri(P(cx-hx,cy-iy,cz-iz), P(cx-ix,cy-iy,cz-hz), P(cx-ix,cy-hy,cz-iz));
    }

    // ---------------------------------------------------------------- Cylinder

    public static void Cylinder(Builder b, Point3D baseCentre, Vector3D axis, double radius, double height,
        int segments = 24, bool capStart = true, bool capEnd = true, double topRadius = double.NaN)
    {
        if (double.IsNaN(topRadius)) topRadius = radius;
        axis.Normalize();
        var (u, v) = Basis(axis);
        var top = baseCentre + axis * height;

        for (int i = 0; i < segments; i++)
        {
            double a0 = 2 * Math.PI * i / segments;
            double a1 = 2 * Math.PI * (i + 1) / segments;
            var d0 = u * Math.Cos(a0) + v * Math.Sin(a0);
            var d1 = u * Math.Cos(a1) + v * Math.Sin(a1);

            var p0 = baseCentre + d0 * radius;
            var p1 = baseCentre + d1 * radius;
            var p2 = top + d1 * topRadius;
            var p3 = top + d0 * topRadius;
            b.QuadSmooth(p0, d0, p1, d1, p2, d1, p3, d0);

            if (capStart) b.Tri(baseCentre, p1, p0);
            if (capEnd) b.Tri(top, p3, p2);
        }
    }

    /// <summary>Annular prism: a ring with a hole. The deck plates and collars are built from these.</summary>
    public static void Tube(Builder b, Point3D centre, Vector3D axis, double innerRadius, double outerRadius,
        double height, int segments = 32)
    {
        axis.Normalize();
        var (u, v) = Basis(axis);
        var top = centre + axis * height;

        for (int i = 0; i < segments; i++)
        {
            double a0 = 2 * Math.PI * i / segments;
            double a1 = 2 * Math.PI * (i + 1) / segments;
            var d0 = u * Math.Cos(a0) + v * Math.Sin(a0);
            var d1 = u * Math.Cos(a1) + v * Math.Sin(a1);

            b.QuadSmooth(centre + d0 * outerRadius, d0, centre + d1 * outerRadius, d1,
                         top + d1 * outerRadius, d1, top + d0 * outerRadius, d0);
            b.QuadSmooth(top + d0 * innerRadius, -d0, top + d1 * innerRadius, -d1,
                         centre + d1 * innerRadius, -d1, centre + d0 * innerRadius, -d0);
            b.Quad(top + d0 * innerRadius, top + d1 * innerRadius, top + d1 * outerRadius, top + d0 * outerRadius);
            b.Quad(centre + d0 * outerRadius, centre + d1 * outerRadius, centre + d1 * innerRadius, centre + d0 * innerRadius);
        }
    }

    /// <summary>
    /// The chassis wedge: an arc segment of an annulus with bevelled outer edges. Nine of these
    /// stacked in three rings are what make the machine read as one enclosed object when sealed.
    /// </summary>
    public static void ArcWedge(Builder b, double innerRadius, double outerRadius, double y0, double y1,
        double startDeg, double sweepDeg, int segments = 18, double bevel = 0.055)
    {
        double s = startDeg * Math.PI / 180, w = sweepDeg * Math.PI / 180;
        double ri = innerRadius, ro = outerRadius - bevel;
        double yb = y0 + bevel, yt = y1 - bevel;

        Point3D At(double a, double r, double y) => new(Math.Cos(a) * r, y, Math.Sin(a) * r);
        Vector3D Out(double a) => new(Math.Cos(a), 0, Math.Sin(a));

        for (int i = 0; i < segments; i++)
        {
            double a0 = s + w * i / segments;
            double a1 = s + w * (i + 1) / segments;
            var n0 = Out(a0);
            var n1 = Out(a1);

            // Outer skin.
            b.QuadSmooth(At(a0, ro, yb), n0, At(a1, ro, yb), n1, At(a1, ro, yt), n1, At(a0, ro, yt), n0);
            // Outer top and bottom bevels.
            b.QuadSmooth(At(a0, ro, yt), n0, At(a1, ro, yt), n1, At(a1, outerRadius - bevel * 2, y1), n1, At(a0, outerRadius - bevel * 2, y1), n0);
            b.QuadSmooth(At(a0, outerRadius - bevel * 2, y0), n0, At(a1, outerRadius - bevel * 2, y0), n1, At(a1, ro, yb), n1, At(a0, ro, yb), n0);
            // Inner face.
            b.QuadSmooth(At(a0, ri, yt), -n0, At(a1, ri, yt), -n1, At(a1, ri, yb), -n1, At(a0, ri, yb), -n0);
            // Top and bottom decks.
            b.Quad(At(a0, ri, y1), At(a1, ri, y1), At(a1, outerRadius - bevel * 2, y1), At(a0, outerRadius - bevel * 2, y1));
            b.Quad(At(a0, outerRadius - bevel * 2, y0), At(a1, outerRadius - bevel * 2, y0), At(a1, ri, y0), At(a0, ri, y0));
            // Inner top and bottom bevels.
            b.Quad(At(a0, ri, yt), At(a1, ri, yt), At(a1, ri, y1), At(a0, ri, y1));
            b.Quad(At(a0, ri, y0), At(a1, ri, y0), At(a1, ri, yb), At(a0, ri, yb));
        }

        // Radial end walls, which are what make the wedge read as a removable part.
        double sa = s, ea = s + w;
        foreach (var (a, flip) in new[] { (sa, false), (ea, true) })
        {
            var pts = new[]
            {
                At(a, ri, yb), At(a, ro, yb), At(a, outerRadius - bevel * 2, y0),
                At(a, outerRadius - bevel * 2, y1), At(a, ro, yt), At(a, ri, yt),
                At(a, ri, y1), At(a, ri, y0)
            };
            if (!flip)
            {
                b.Quad(pts[7], pts[0], pts[5], pts[6]);
                b.Quad(pts[0], pts[2], pts[3], pts[5]);
                b.Tri(pts[0], pts[1], pts[2]);
                b.Tri(pts[3], pts[4], pts[5]);
            }
            else
            {
                b.Quad(pts[6], pts[5], pts[0], pts[7]);
                b.Quad(pts[5], pts[3], pts[2], pts[0]);
                b.Tri(pts[2], pts[1], pts[0]);
                b.Tri(pts[5], pts[4], pts[3]);
            }
        }
    }

    /// <summary>Regular prism with n sides, used for the spine core and the foundation plinth.</summary>
    public static void Prism(Builder b, Point3D centre, double radius, double height, int sides,
        double rotationDeg = 0, double topRadius = double.NaN, bool cap = true)
    {
        if (double.IsNaN(topRadius)) topRadius = radius;
        double rot = rotationDeg * Math.PI / 180;
        var top = new Point3D(centre.X, centre.Y + height, centre.Z);

        for (int i = 0; i < sides; i++)
        {
            double a0 = rot + 2 * Math.PI * i / sides;
            double a1 = rot + 2 * Math.PI * (i + 1) / sides;
            var p0 = new Point3D(centre.X + Math.Cos(a0) * radius, centre.Y, centre.Z + Math.Sin(a0) * radius);
            var p1 = new Point3D(centre.X + Math.Cos(a1) * radius, centre.Y, centre.Z + Math.Sin(a1) * radius);
            var p2 = new Point3D(top.X + Math.Cos(a1) * topRadius, top.Y, top.Z + Math.Sin(a1) * topRadius);
            var p3 = new Point3D(top.X + Math.Cos(a0) * topRadius, top.Y, top.Z + Math.Sin(a0) * topRadius);
            b.Quad(p0, p1, p2, p3);
            if (cap)
            {
                b.Tri(top, p3, p2);
                b.Tri(centre, p1, p0);
            }
        }
    }

    public static void Torus(Builder b, Point3D centre, Vector3D axis, double majorRadius, double minorRadius,
        int major = 28, int minor = 10)
    {
        axis.Normalize();
        var (u, v) = Basis(axis);

        Point3D Pt(int i, int j)
        {
            double a = 2 * Math.PI * i / major;
            double t = 2 * Math.PI * j / minor;
            var ring = u * Math.Cos(a) + v * Math.Sin(a);
            var pos = centre + ring * (majorRadius + minorRadius * Math.Cos(t)) + axis * (minorRadius * Math.Sin(t));
            return pos;
        }

        Vector3D Nm(int i, int j)
        {
            double a = 2 * Math.PI * i / major;
            double t = 2 * Math.PI * j / minor;
            var ring = u * Math.Cos(a) + v * Math.Sin(a);
            var n = ring * Math.Cos(t) + axis * Math.Sin(t);
            n.Normalize();
            return n;
        }

        for (int i = 0; i < major; i++)
            for (int j = 0; j < minor; j++)
            {
                int i1 = (i + 1) % major, j1 = (j + 1) % minor;
                b.QuadSmooth(Pt(i, j), Nm(i, j), Pt(i1, j), Nm(i1, j), Pt(i1, j1), Nm(i1, j1), Pt(i, j1), Nm(i, j1));
            }
    }

    /// <summary>Hemisphere for the sensor dome.</summary>
    public static void Dome(Builder b, Point3D centre, double radius, int segments = 20, int rings = 8)
    {
        for (int r = 0; r < rings; r++)
        {
            double p0 = Math.PI / 2 * r / rings;
            double p1 = Math.PI / 2 * (r + 1) / rings;
            for (int s = 0; s < segments; s++)
            {
                double a0 = 2 * Math.PI * s / segments;
                double a1 = 2 * Math.PI * (s + 1) / segments;
                Vector3D N(double phi, double a) => new(Math.Cos(phi) * Math.Cos(a), Math.Sin(phi), Math.Cos(phi) * Math.Sin(a));
                var n00 = N(p0, a0); var n01 = N(p0, a1); var n10 = N(p1, a0); var n11 = N(p1, a1);
                b.QuadSmooth(centre + n00 * radius, n00, centre + n01 * radius, n01,
                             centre + n11 * radius, n11, centre + n10 * radius, n10);
            }
        }
    }

    /// <summary>A unit cylinder from origin along +Y with height 1 and radius 1, reused by every link.</summary>
    public static MeshGeometry3D UnitCylinder(int segments = 12)
    {
        var b = new Builder();
        Cylinder(b, new Point3D(0, 0, 0), new Vector3D(0, 1, 0), 1, 1, segments);
        return b.ToMesh();
    }

    public static MeshGeometry3D UnitSphere(int segments = 12, int rings = 8)
    {
        var b = new Builder();
        for (int r = 0; r < rings; r++)
        {
            double p0 = -Math.PI / 2 + Math.PI * r / rings;
            double p1 = -Math.PI / 2 + Math.PI * (r + 1) / rings;
            for (int s = 0; s < segments; s++)
            {
                double a0 = 2 * Math.PI * s / segments;
                double a1 = 2 * Math.PI * (s + 1) / segments;
                Vector3D N(double phi, double a) => new(Math.Cos(phi) * Math.Cos(a), Math.Sin(phi), Math.Cos(phi) * Math.Sin(a));
                var n00 = N(p0, a0); var n01 = N(p0, a1); var n10 = N(p1, a0); var n11 = N(p1, a1);
                b.QuadSmooth((Point3D)n00, n00, (Point3D)n01, n01, (Point3D)n11, n11, (Point3D)n10, n10);
            }
        }
        return b.ToMesh();
    }

    /// <summary>Two perpendicular vectors completing a basis with <paramref name="axis"/>.</summary>
    private static (Vector3D U, Vector3D V) Basis(Vector3D axis)
    {
        var reference = Math.Abs(axis.Y) > 0.92 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var u = Vector3D.CrossProduct(reference, axis);
        u.Normalize();
        var v = Vector3D.CrossProduct(axis, u);
        v.Normalize();
        return (u, v);
    }

    public static Point3D Polar(double azimuthDeg, double radius, double y)
    {
        double a = azimuthDeg * Math.PI / 180;
        return new Point3D(Math.Cos(a) * radius, y, Math.Sin(a) * radius);
    }

    public static Vector3D Radial(double azimuthDeg)
    {
        double a = azimuthDeg * Math.PI / 180;
        return new Vector3D(Math.Cos(a), 0, Math.Sin(a));
    }

    /// <summary>Tangential direction at an azimuth: the axis a wedge tilts about.</summary>
    public static Vector3D Tangent(double azimuthDeg)
    {
        double a = azimuthDeg * Math.PI / 180;
        return new Vector3D(-Math.Sin(a), 0, Math.Cos(a));
    }
}
