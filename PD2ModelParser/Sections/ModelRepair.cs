using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace PD2ModelParser.Sections
{
    internal static class ModelRepair
    {

        // Stuff to run to repair possible failures.
        public static int RepairAll(FullModelData data)
        {
            var models = data.SectionsOfType<Model>().ToList();
            int repaired = 0;
            foreach (var group in models.Where(m => m.Version != 6 &&
                         m.PassthroughGP?.Topology?.Primitive_type == Topology.PrimitiveType.PointList)
                         .GroupBy(m => m.PassthroughGP.Topology))
            {
                if (!group.All(HasTriangleEvidence))
                    continue;

                group.Key.Primitive_type = Topology.PrimitiveType.TriangleList;
                Log.Default.Warn("Repairing .model topology {0}: PointList -> TriangleList ({1} triangles)",
                    group.Key.SectionId, group.Key.facelist.Count);
                repaired++;
            }

            foreach (var model in models)
                repaired += RepairRenderAtomMetadata(model);

            return repaired;
        }

        // Legacy Tool wrote .model sections with PointList topology instead of TriangleList.
        // Check for valid mesh data and repair the topology.
        private static bool HasTriangleEvidence(Model model)
        {
            var gp = model.PassthroughGP;
            var topology = gp?.Topology;
            var geometry = gp?.DieselGeometry;
            if (topology == null || geometry == null || model.TopologyIP?.Topology != topology ||
                topology.facelist == null || topology.facelist.Count == 0 ||
                geometry.verts == null || geometry.verts.Count == 0 ||
                model.RenderAtoms == null || model.RenderAtoms.Count == 0)
                return false;

            ulong totalIndices = (ulong)topology.facelist.Count * 3;
            int vertexCount = geometry.verts.Count;
            var spans = new List<(int Start, int End)>();
            bool nondegenerate = false;

            foreach (var atom in model.RenderAtoms)
            {
                if (atom.TriangleCount == 0)
                    continue;

                ulong startIndex = atom.BaseIndex;
                ulong endIndex = startIndex + (ulong)atom.TriangleCount * 3;
                if (startIndex % 3 != 0 || endIndex > totalIndices)
                    return false;

                int start = (int)(startIndex / 3);
                int end = (int)(endIndex / 3);
                bool local = true;
                bool absolute = true;
                for (int i = start; i < end; i++)
                {
                    var face = topology.facelist[i];
                    foreach (int index in new[] { (int)face.a, (int)face.b, (int)face.c })
                    {
                        local &= (ulong)index + atom.BaseVertex < (ulong)vertexCount;
                        absolute &= index < vertexCount;
                    }
                }

                if ((!local && !absolute) || (local && absolute && atom.BaseVertex != 0))
                    return false;

                int vertexOffset = local && !absolute ? (int)atom.BaseVertex : 0;
                for (int i = start; i < end && !nondegenerate; i++)
                {
                    var face = topology.facelist[i];
                    var a = geometry.verts[(int)face.a + vertexOffset];
                    var b = geometry.verts[(int)face.b + vertexOffset];
                    var c = geometry.verts[(int)face.c + vertexOffset];
                    float areaSquared = Vector3.Cross(b - a, c - a).LengthSquared();
                    if (float.IsFinite(areaSquared) && areaSquared > 1e-16f)
                        nondegenerate = true;
                }

                spans.Add((start, end));
            }

            if (!nondegenerate || spans.Count == 0)
                return false;

            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            int covered = 0;
            foreach (var span in spans)
            {
                if (span.Start != covered)
                    return false;
                covered = span.End;
            }

            return covered == topology.facelist.Count;
        }

        // Model data was injected incorrectly leaving RenderAtom metadata inconsistent with the actual mesh data.
        // Check for valid mesh data and repair the meta data.
        private static int RepairRenderAtomMetadata(Model model)
        {
            if (model.Version == 6 || model.RenderAtoms == null || model.RenderAtoms.Count == 0 || model.PassthroughGP == null)
                return 0;

            var topology = model.PassthroughGP.Topology;
            var geometry = model.PassthroughGP.DieselGeometry;
            if (topology == null || geometry == null ||
                topology.Primitive_type != Topology.PrimitiveType.TriangleList)
                return 0;

            uint totalIndices = checked((uint)topology.facelist.Count * 3u);
            uint totalVertices = geometry.vert_count;
            int repaired = 0;

            for (int i = 0; i < model.RenderAtoms.Count; i++)
            {
                var atom = model.RenderAtoms[i];

                uint indexStart = Math.Min(atom.BaseIndex, totalIndices);
                uint indexEnd = totalIndices;
                for (int j = 0; j < model.RenderAtoms.Count; j++)
                {
                    uint candidate = model.RenderAtoms[j].BaseIndex;
                    if (candidate > indexStart && candidate < indexEnd)
                        indexEnd = candidate;
                }

                uint triangleCount = (indexEnd - indexStart) / 3u;

                uint vertexStart = Math.Min(atom.BaseVertex, totalVertices);
                uint vertexEnd = totalVertices;
                for (int j = 0; j < model.RenderAtoms.Count; j++)
                {
                    uint candidate = model.RenderAtoms[j].BaseVertex;
                    if (candidate > vertexStart && candidate < vertexEnd)
                        vertexEnd = candidate;
                }
                uint sliceLength = vertexEnd - vertexStart;

                if (atom.TriangleCount == triangleCount && atom.GeometrySliceLength == sliceLength)
                    continue;

                Log.Default.Warn(
                    "Repairing .model RenderAtom metadata: model={0}, atom={1}, TriangleCount {2}->{3}, GeometrySliceLength {4}->{5}",
                    model.Name, i, atom.TriangleCount, triangleCount, atom.GeometrySliceLength, sliceLength);

                atom.TriangleCount = triangleCount;
                atom.GeometrySliceLength = sliceLength;
                repaired++;
            }

            return repaired;
        }

    }
}
