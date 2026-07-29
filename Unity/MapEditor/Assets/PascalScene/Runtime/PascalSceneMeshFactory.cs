using System;
using System.Collections.Generic;
using UnityEngine;

namespace PascalScene
{
    public static class PascalSceneMeshFactory
    {
        private const float Epsilon = 0.00001f;

        public static Mesh CreateWall(
            float[] start,
            float[] end,
            float height,
            float thickness,
            float curveOffset = 0f,
            float baseElevation = 0f)
        {
            RequirePoint(start, nameof(start));
            RequirePoint(end, nameof(end));
            if (height <= 0f || thickness <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height), "Wall height and thickness must be positive.");
            }

            var a = new Vector2(start[0], start[1]);
            var b = new Vector2(end[0], end[1]);
            var direction = b - a;
            if (direction.sqrMagnitude <= Epsilon * Epsilon)
            {
                throw new ArgumentException("Wall start and end must be different.");
            }

            if (Mathf.Abs(curveOffset) > Epsilon)
            {
                return CreateCurvedWall(a, b, height, thickness, curveOffset, baseElevation);
            }

            var perpendicular = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
            return CreatePolygonPrism(
                new List<Vector2>
                {
                    a + perpendicular,
                    b + perpendicular,
                    b - perpendicular,
                    a - perpendicular
                },
                baseElevation,
                baseElevation + height,
                "Pascal Wall Mesh");
        }

        public static Mesh CreateWallFromFootprint(
            IReadOnlyList<Vector2> footprint,
            float height,
            float baseElevation)
        {
            if (height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            return CreatePolygonPrism(
                footprint,
                baseElevation,
                baseElevation + height,
                "Pascal Mitered Wall Mesh");
        }

        private static Mesh CreateCurvedWall(
            Vector2 start,
            Vector2 end,
            float height,
            float thickness,
            float curveOffset,
            float baseElevation)
        {
            var chord = end - start;
            var chordLength = chord.magnitude;
            var sagitta = curveOffset;
            if (chordLength <= Epsilon || Mathf.Abs(sagitta) <= Epsilon)
            {
                throw new ArgumentException("Curved wall requires a non-zero chord and curve offset.");
            }

            var direction = Mathf.Sign(sagitta);
            var normal = new Vector2(-chord.y, chord.x).normalized;
            var radius = (chordLength * chordLength) / (8f * Mathf.Abs(sagitta)) + Mathf.Abs(sagitta) * 0.5f;
            var midpoint = (start + end) * 0.5f;
            var center = midpoint + normal * (radius - Mathf.Abs(sagitta)) * direction;
            var startAngle = Mathf.Atan2(start.y - center.y, start.x - center.x);
            var endAngle = Mathf.Atan2(end.y - center.y, end.x - center.x);
            var delta = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (direction > 0f)
            {
                while (delta <= 0f) delta += Mathf.PI * 2f;
            }
            else
            {
                while (delta >= 0f) delta -= Mathf.PI * 2f;
            }
            var segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * radius / 0.2f), 8, 64);
            var outer = new List<Vector2>(segments + 1);
            var inner = new List<Vector2>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                var angle = startAngle + delta * i / segments;
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outer.Add(center + radial * (radius + thickness * 0.5f));
                inner.Add(center + radial * (radius - thickness * 0.5f));
            }

            inner.Reverse();
            outer.AddRange(inner);
            return CreatePolygonPrism(outer, baseElevation, baseElevation + height, "Pascal Curved Wall Mesh");
        }

        public static Mesh CreatePolygonPrism(
            IReadOnlyList<Vector2> polygon,
            float bottom,
            float top,
            string meshName = "Pascal Polygon Prism",
            IReadOnlyList<IReadOnlyList<Vector2>> holes = null)
        {
            var points = MergeHoles(CleanPolygon(polygon), holes);
            if (top <= bottom)
            {
                throw new ArgumentException("Prism top must be above its bottom.");
            }

            var surfaceTriangles = Triangulate(points);
            var count = points.Count;
            var vertices = new Vector3[count * 2];
            for (var i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(points[i].x, top, points[i].y);
                vertices[count + i] = new Vector3(points[i].x, bottom, points[i].y);
            }

            var triangles = new List<int>(surfaceTriangles.Count * 2 + count * 6);
            var isCounterClockwise = SignedArea(points) > 0f;
            for (var i = 0; i < surfaceTriangles.Count; i += 3)
            {
                var a = surfaceTriangles[i];
                var b = surfaceTriangles[i + 1];
                var c = surfaceTriangles[i + 2];
                if (isCounterClockwise)
                {
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(a);
                    triangles.Add(count + a);
                    triangles.Add(count + b);
                    triangles.Add(count + c);
                }
                else
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(count + c);
                    triangles.Add(count + b);
                    triangles.Add(count + a);
                }
            }

            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                if (isCounterClockwise)
                {
                    AddTriangle(triangles, count + i, i, next);
                    AddTriangle(triangles, count + i, next, count + next);
                }
                else
                {
                    AddTriangle(triangles, count + i, next, i);
                    AddTriangle(triangles, count + i, count + next, next);
                }
            }

            return CreateMesh(meshName, vertices, triangles);
        }

        public static Mesh CreateDoubleSidedSurface(
            IReadOnlyList<Vector2> polygon,
            float elevation,
            string meshName = "Pascal Surface",
            IReadOnlyList<IReadOnlyList<Vector2>> holes = null)
        {
            var points = MergeHoles(CleanPolygon(polygon), holes);
            var surfaceTriangles = Triangulate(points);
            var vertices = new Vector3[points.Count * 2];
            for (var i = 0; i < points.Count; i++)
            {
                vertices[i] = new Vector3(points[i].x, elevation, points[i].y);
                vertices[points.Count + i] = vertices[i];
            }

            var triangles = new List<int>(surfaceTriangles.Count * 2);
            var isCounterClockwise = SignedArea(points) > 0f;
            for (var i = 0; i < surfaceTriangles.Count; i += 3)
            {
                var a = surfaceTriangles[i];
                var b = surfaceTriangles[i + 1];
                var c = surfaceTriangles[i + 2];
                if (isCounterClockwise)
                {
                    AddTriangle(triangles, c, b, a);
                    AddTriangle(
                        triangles,
                        points.Count + a,
                        points.Count + b,
                        points.Count + c);
                }
                else
                {
                    AddTriangle(triangles, a, b, c);
                    AddTriangle(
                        triangles,
                        points.Count + c,
                        points.Count + b,
                        points.Count + a);
                }
            }

            return CreateMesh(meshName, vertices, triangles);
        }

        public static List<int> Triangulate(IReadOnlyList<Vector2> polygon)
        {
            var points = CleanPolygon(polygon);
            var remaining = new List<int>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                remaining.Add(i);
            }

            var triangles = new List<int>((points.Count - 2) * 3);
            var counterClockwise = SignedArea(points) > 0f;
            var guard = points.Count * points.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                var earFound = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var previous = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    var current = remaining[i];
                    var next = remaining[(i + 1) % remaining.Count];
                    var cross = Cross(points[previous], points[current], points[next]);
                    if ((counterClockwise && cross <= Epsilon) ||
                        (!counterClockwise && cross >= -Epsilon))
                    {
                        continue;
                    }

                    var containsPoint = false;
                    for (var candidateIndex = 0; candidateIndex < remaining.Count; candidateIndex++)
                    {
                        var candidate = remaining[candidateIndex];
                        if (candidate == previous || candidate == current || candidate == next)
                        {
                            continue;
                        }

                        if ((points[candidate] - points[previous]).sqrMagnitude <= Epsilon * Epsilon ||
                            (points[candidate] - points[current]).sqrMagnitude <= Epsilon * Epsilon ||
                            (points[candidate] - points[next]).sqrMagnitude <= Epsilon * Epsilon)
                        {
                            continue;
                        }

                        if (PointInTriangle(
                                points[candidate],
                                points[previous],
                                points[current],
                                points[next]))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                    {
                        continue;
                    }

                    triangles.Add(previous);
                    triangles.Add(current);
                    triangles.Add(next);
                    remaining.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    throw new ArgumentException(
                        "Polygon cannot be triangulated. It may be self-intersecting or degenerate.");
                }
            }

            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]);
                triangles.Add(remaining[1]);
                triangles.Add(remaining[2]);
            }

            return triangles;
        }

        private static List<Vector2> CleanPolygon(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            var points = new List<Vector2>(polygon.Count);
            foreach (var point in polygon)
            {
                if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > Epsilon * Epsilon)
                {
                    points.Add(point);
                }
            }

            if (points.Count > 1 &&
                (points[0] - points[points.Count - 1]).sqrMagnitude <= Epsilon * Epsilon)
            {
                points.RemoveAt(points.Count - 1);
            }

            if (points.Count < 3 || Mathf.Abs(SignedArea(points)) <= Epsilon)
            {
                throw new ArgumentException("Polygon must contain at least three non-collinear points.");
            }

            return points;
        }

        private static List<Vector2> MergeHoles(
            List<Vector2> outer,
            IReadOnlyList<IReadOnlyList<Vector2>> holes)
        {
            if (holes == null || holes.Count == 0)
            {
                return outer;
            }

            var merged = new List<Vector2>(outer);
            foreach (var rawHole in holes)
            {
                var hole = CleanPolygon(rawHole);
                if (SignedArea(hole) > 0f)
                {
                    hole.Reverse();
                }

                var holeIndex = 0;
                for (var i = 1; i < hole.Count; i++)
                {
                    if (hole[i].x > hole[holeIndex].x)
                    {
                        holeIndex = i;
                    }
                }

                var outerIndex = FindVisibleOuterVertex(merged, hole[holeIndex], hole);
                if (outerIndex < 0)
                {
                    throw new ArgumentException("Polygon hole cannot be bridged to its outer contour.");
                }

                var bridged = new List<Vector2>(merged.Count + hole.Count + 2);
                for (var i = 0; i <= outerIndex; i++) bridged.Add(merged[i]);
                for (var i = 0; i <= hole.Count; i++) bridged.Add(hole[(holeIndex + i) % hole.Count]);
                bridged.Add(merged[outerIndex]);
                for (var i = outerIndex + 1; i < merged.Count; i++) bridged.Add(merged[i]);
                merged = bridged;
            }

            return merged;
        }

        private static int FindVisibleOuterVertex(
            IReadOnlyList<Vector2> outer,
            Vector2 holePoint,
            IReadOnlyList<Vector2> hole)
        {
            var bestIndex = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < outer.Count; i++)
            {
                var candidate = outer[i];
                if (candidate.x + Epsilon < holePoint.x)
                {
                    continue;
                }

                var blocked = false;
                for (var edge = 0; edge < outer.Count; edge++)
                {
                    var next = (edge + 1) % outer.Count;
                    if (edge == i || next == i) continue;
                    if (SegmentsIntersect(holePoint, candidate, outer[edge], outer[next]))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                {
                    var distance = (candidate - holePoint).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }
            }

            return bestIndex;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            return ((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
                   ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon));
        }

        private static Mesh CreateMesh(string name, Vector3[] vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            var area = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                area += points[i].x * points[next].y - points[next].x * points[i].y;
            }

            return area * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(a, b, point);
            var bc = Cross(b, c, point);
            var ca = Cross(c, a, point);
            var hasNegative = ab < -Epsilon || bc < -Epsilon || ca < -Epsilon;
            var hasPositive = ab > Epsilon || bc > Epsilon || ca > Epsilon;
            return !(hasNegative && hasPositive);
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static void RequirePoint(float[] point, string parameterName)
        {
            if (point == null || point.Length < 2)
            {
                throw new ArgumentException("Expected an [x, z] point.", parameterName);
            }
        }
    }
}
