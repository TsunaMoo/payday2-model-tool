using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GLTF = SharpGLTF.Schema2;
using DM = PD2ModelParser.Sections;
using System.Reflection;

namespace PD2ModelParser.Importers
{
    class GltfImporter
    {
        public static void Import(FullModelData fmd, string path, bool createModels, Func<string, DM.Object3D> parentFinder, IOptionReceiver opts)
        {
            GLTF.ModelRoot gltf = null;
            try
            {
                gltf = GLTF.ModelRoot.Load(path);
            }
            catch (SharpGLTF.Validation.DataException)
            {
                if (!TryLoadWithoutTangents(path, out gltf)) throw;
            }
            var importer = new GltfImporter(fmd);
            string preserveSkinsOpt = opts.GetOption("overwrite-rigging");
            if (preserveSkinsOpt != null)
            {
                importer.overwriteRigging = bool.Parse(preserveSkinsOpt);
            }

            bool TryLoadWithoutTangents(string path, out GLTF.ModelRoot root)
            {
                root = null;
                try
                {
                    var data = System.IO.File.ReadAllBytes(path);
                    if (data.Length < 20) return false;
                    // GLB header
                    uint magic = BitConverter.ToUInt32(data, 0);
                    if (magic != 0x46546C67) return false; // 'glTF'
                    int offset = 12;
                    uint chunkLen = BitConverter.ToUInt32(data, offset);
                    uint chunkType = BitConverter.ToUInt32(data, offset + 4);
                    offset += 8;
                    if (chunkType != 0x4E4F534A) return false; // JSON
                    var json = System.Text.Encoding.UTF8.GetString(data, offset, (int)chunkLen);
                    var j = Newtonsoft.Json.Linq.JObject.Parse(json);
                    bool modified = false;
                    var meshes = j["meshes"] as Newtonsoft.Json.Linq.JArray;
                    if (meshes != null)
                    {
                        foreach (var mesh in meshes)
                        {
                            var prims = mesh["primitives"] as Newtonsoft.Json.Linq.JArray;
                            if (prims == null) continue;
                            foreach (var prim in prims)
                            {
                                var attrs = prim["attributes"] as Newtonsoft.Json.Linq.JObject;
                                if (attrs != null && attrs.Property("TANGENT") != null)
                                {
                                    attrs.Property("TANGENT").Remove();
                                    modified = true;
                                }
                            }
                        }
                    }

                    if (!modified) return false;

                    var newJson = j.ToString(Newtonsoft.Json.Formatting.None);
                    var newJsonBytes = System.Text.Encoding.UTF8.GetBytes(newJson);
                    int pad = (4 - (newJsonBytes.Length % 4)) % 4;
                    var padded = new byte[newJsonBytes.Length + pad];
                    Array.Copy(newJsonBytes, padded, newJsonBytes.Length);

                    using (var ms = new System.IO.MemoryStream())
                    {
                        ms.Write(BitConverter.GetBytes(0x46546C67), 0, 4);
                        ms.Write(BitConverter.GetBytes(2u), 0, 4);
                        ms.Write(BitConverter.GetBytes(0u), 0, 4);
                        ms.Write(BitConverter.GetBytes((uint)padded.Length), 0, 4);
                        ms.Write(BitConverter.GetBytes(0x4E4F534A), 0, 4);
                        ms.Write(padded, 0, padded.Length);
                        int jsonEnd = offset + (int)chunkLen;
                        if (jsonEnd < data.Length)
                        {
                            ms.Write(data, jsonEnd, data.Length - jsonEnd);
                        }
                        ms.Seek(8, System.IO.SeekOrigin.Begin);
                        ms.Write(BitConverter.GetBytes((uint)ms.Length), 0, 4);
                        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetFileNameWithoutExtension(path) + "_notangent.glb");
                        System.IO.File.WriteAllBytes(tmp, ms.ToArray());
                        root = GLTF.ModelRoot.Load(tmp);
                        return true;
                    }
                }
                catch { return false; }
            }

            string importTransforms = opts.GetOption("import-transforms");
            if (importTransforms != null)
            {
                bool.TryParse(importTransforms, out importer.importTransforms);
            }

            foreach (var mesh in gltf.LogicalMeshes)
                foreach (var prim in mesh.Primitives)
                    TryGenerateTangentsUsingToolkit(prim);

            importer.ImportTree(gltf, createModels, parentFinder);
        }

        public static bool ReuseExistingObjects = false;
        FullModelData data;
        Dictionary<GLTF.Node, DM.Object3D> objectsByNode = new Dictionary<GLTF.Node, DM.Object3D>();
        bool createModels;
        bool overwriteRigging;
        bool importTransforms = true;
        List<(GLTF.Node node, DM.Model model)> toSkin = new List<(GLTF.Node node, DM.Model model)>();
        List<(GLTF.Skin skin, DM.Model model)> toRemap = new List<(GLTF.Skin skin, DM.Model model)>();

        /// <summary>
        /// How much to embiggen incoming GLTF data.
        /// </summary>
        /// <remarks>
        /// GLTF specifies a 1m scale and Diesel uses 1cm, so the default of 100 should normally
        /// be useful.
        /// 
        /// The proof re how is longwinded, but if you work out how "apply scale" has to work
        /// when all transformations are translate-rotate-scale (as in GLTF), you can discover that
        /// because uniform scale commutes with rotate and with any scale, and it commutes with
        /// translation by multiplying or dividing the translation's vector by the scale factor,
        /// all we need to do is 1) scale every mesh on import, and 2) twiddle the translation
        /// component of every node on import.
        /// </remarks>
        float scaleFactor = 100;
        Matrix4x4 axisCorrection = Matrix4x4.CreateRotationX(MathF.PI / 2);

        public GltfImporter(FullModelData data)
        {
            this.data = data;
        }

        private static bool TryGenerateTangentsUsingToolkit(GLTF.MeshPrimitive prim)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies)
                {
                    if (!asm.GetName().Name.Contains("Toolkit") && !asm.GetName().Name.Contains("SharpGLTF")) continue;
                    Type[] types = null;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        // Look for any public static method with 'Tangent' in the name and first parameter assignable from MeshPrimitive
                        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
                        foreach (var m in methods)
                        {
                            if (!m.Name.ToLowerInvariant().Contains("tangent")) continue;
                            var pars = m.GetParameters();
                            if (pars.Length == 0) continue;
                            if (!pars[0].ParameterType.IsAssignableFrom(typeof(GLTF.MeshPrimitive))) continue;
                            try
                            {
                                var args = new object[pars.Length];
                                args[0] = prim;
                                // If method has additional optional parameters, we leave them as default/null
                                for (int i = 1; i < pars.Length; i++) args[i] = Type.Missing;
                                m.Invoke(null, args);
                                return true;
                            }
                            catch { continue; }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public void ImportTree(GLTF.ModelRoot root, bool createModels, Func<string, DM.Object3D> parentFinder)
        {
            this.createModels = createModels;

            foreach (var node in root.DefaultScene.VisualChildren)
            {
                DM.Object3D parent = null;
                try
                {
                    parent = parentFinder(node.Name);
                }
                catch { }

                ImportNode(node, parent, axisCorrection);
            }

            foreach (var i in toSkin)
            {
                ImportSkin(i.node, i.model);
            }

            foreach (var i in toRemap)
            {
                RemapBoneIds(i.skin, i.model);
            }

            ImportAnimations(root);
        }

        void ImportNode(GLTF.Node node, DM.Object3D parent, Matrix4x4 parentCorrection)
        {
            var hashname = HashName.FromNumberOrString(node.Name);

            DM.Object3D obj = null;

            if (ReuseExistingObjects)
            {
                obj = data.parsed_sections
                    .Select(i => i.Value as DM.Object3D)
                    .Where(i => i != null)
                    .FirstOrDefault(i => i.HashName.Hash == hashname.Hash);
            }

            bool shouldSetParent = true;

            if (obj == null)
            {
                if (createModels && node.Mesh == null)
                {
                    obj = new DM.Object3D(hashname.String, parent);
                }
                else if (createModels && node.Mesh != null)
                {
                    obj = CreateNewModel(node.Mesh, node.Name);

                    if (node.Skin != null)
                    {
                        toSkin.Add((node, obj as DM.Model));
                        toRemap.Add((node.Skin, obj as DM.Model));
                    }
                }
                else if (createModels && node.PunctualLight != null)
                {
                    obj = CreateNewLamp(node.PunctualLight, node.Name);
                }
                else
                {
                    throw new Exception($"Object {node.Name} does not already exist and object creation is disabled.");
                }

                data.AddSection(obj);
            }
            else
            {
                if (node.Mesh != null && !(obj is DM.Model))
                {
                    if (!createModels)
                    {
                        throw new Exception($"Object {node.Name} already exists, isn't a model, and object creation is disabled.");
                    }

                    var oldObj = obj;
                    obj = CreateNewModel(node.Mesh, node.Name);

                    foreach (var i in oldObj.children.ToList())
                    {
                        i.SetParent(obj);
                    }

                    data.AddSection(obj);

                    if (node.Skin != null)
                    {
                        toSkin.Add((node, obj as DM.Model));
                        toRemap.Add((node.Skin, obj as DM.Model));
                    }
                }
                else if (node.Mesh != null && obj is DM.Model mod)
                {
                    OverwriteModel(node.Mesh, mod);

                    if (node.Skin != null)
                    {
                        if (overwriteRigging)
                            toSkin.Add((node, mod));

                        toRemap.Add((node.Skin, mod));
                    }
                }
                else if (node.PunctualLight != null)
                {
                    throw new Exception("Can't overwrite lights yet.");
                }

                shouldSetParent = false;
            }

            objectsByNode.Add(node, obj);

            if (parent != null && shouldSetParent)
            {
                obj.SetParent(parent);
            }

            if (importTransforms)
            {
                var lt = node.LocalTransform;
                var m = lt.Matrix;

                m.M41 *= scaleFactor;
                m.M42 *= scaleFactor;
                m.M43 *= scaleFactor;

                if (parentCorrection != Matrix4x4.Identity)
                    m = parentCorrection * m;

                obj.Transform = m;
            }

            (obj as DM.Model)?.UpdateBounds();

            foreach (var child in node.VisualChildren)
            {
                ImportNode(child, obj, Matrix4x4.Identity);
            }
        }

        void OverwriteModel(GLTF.Mesh gmesh, DM.Model model)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            var mats = md.materials.Select(i =>
            {
                var hn = HashName.FromNumberOrString(i);
                var mat = data.SectionsOfType<DM.Material>().FirstOrDefault(j => j.HashName.Hash == hn.Hash);
                if (mat == null)
                {
                    mat = new DM.Material(i);
                    data.AddSection(mat);
                }
                return mat;
            }).ToList();

            var matGroup = new DM.MaterialGroup(mats);
            data.AddSection(matGroup);
            model.MaterialGroup = matGroup;

            var ms = new MeshSections();
            ms.topoip = model.TopologyIP;
            ms.passgp = model.PassthroughGP;
            ms.geom = ms.passgp.Geometry;
            ms.topo = ms.passgp.Topology;
            ms.atoms = md.renderAtoms;

            ms.PopulateFromMeshData(md);
            ms.Scale(this.scaleFactor);

            model.RenderAtoms = md.renderAtoms;
        }

        DM.Light CreateNewLamp(GLTF.PunctualLight gl, string name)
        {
            throw new NotImplementedException("Lights are currently not implemented");
        }

        DM.Model CreateNewModel(GLTF.Mesh gmesh, string name)
        {
            var md = MeshData.FromGltfMesh(gmesh);

            var mats = md.materials.Select(i =>
            {
                var hn = HashName.FromNumberOrString(i);
                var mat = data.SectionsOfType<DM.Material>().FirstOrDefault(j => j.HashName.Hash == hn.Hash);
                if (mat == null)
                {
                    mat = new DM.Material(i);
                    data.AddSection(mat);
                }
                return mat;
            }).ToList();

            var matGroup = new DM.MaterialGroup(mats);
            data.AddSection(matGroup);

            var ms = new MeshSections();

            ms.geom = new DM.Geometry();
            data.AddSection(ms.geom);
            ms.geom.HashName = new HashName(gmesh.Name + ".Geometry");

            ms.topo = new DM.Topology(gmesh.Name);
            data.AddSection(ms.topo);

            ms.topoip = new DM.TopologyIP(ms.topo);
            data.AddSection(ms.topoip);

            ms.passgp = new DM.PassthroughGP(ms.geom, ms.topo);
            data.AddSection(ms.passgp);

            ms.atoms = md.renderAtoms;

            ms.PopulateFromMeshData(md);
            ms.Scale(this.scaleFactor);

            var model = new DM.Model(name, (uint)ms.geom.verts.Count, (uint)ms.topo.facelist.Count, ms.passgp, ms.topoip, matGroup, null);
            model.RenderAtoms = md.renderAtoms;

            return model;
        }

        DM.Model CreateNewModelv6(GLTF.Node node, DM.Object3D parent)
        {
            throw new NotImplementedException("Creating v6 models is not currently supported");
        }

        void ImportSkin(GLTF.Node node, DM.Model model)
        {
            DM.SkinBones skinBones = new DM.SkinBones();

            skinBones.global_skin_transform = Matrix4x4.Identity;

            var skeletonNode = node.Skin.Skeleton;

            if (skeletonNode == null)
            {
                throw new Exception(
                    $"Skinned model \"{model.Name}\" has no GLTF skeleton root.");
            }

            if (!objectsByNode.TryGetValue(skeletonNode, out var skeletonRoot))
            {
                throw new Exception(
                    $"GLTF skeleton root \"{skeletonNode.Name}\" " +
                    $"was not imported as an Object3D.");
            }

            skinBones.ProbablyRootBone = skeletonRoot;

            DM.Geometry geom = model.PassthroughGP.Geometry;

            if (geom.weight_groups.Count != geom.vert_count)
            {
                throw new Exception(
                    $"Model \"{model.Name}\" has {geom.vert_count} vertices but " +
                    $"{geom.weight_groups.Count} weight-group entries.");
            }

            if (geom.weights.Count != geom.vert_count)
            {
                throw new Exception(
                    $"Model \"{model.Name}\" has {geom.vert_count} vertices but " +
                    $"{geom.weights.Count} weight entries.");
            }

            HashSet<ushort> usedBones = new HashSet<ushort>();
            const float threshold = 0.00001f;

            for (int i = 0; i < geom.vert_count; i++)
            {
                var group = geom.weight_groups[i];
                var weights = geom.weights[i];

                if (weights.X > threshold)
                    usedBones.Add(group.Bones1);

                if (weights.Y > threshold)
                    usedBones.Add(group.Bones2);

                if (weights.Z > threshold)
                    usedBones.Add(group.Bones3);
            }

            var bmi = new DM.BoneMappingItem();

            foreach (ushort gltfId in usedBones.OrderBy(i => i))
            {
                if (gltfId >= node.Skin.JointsCount)
                {
                    throw new Exception(
                        $"Model \"{model.Name}\" references GLTF joint {gltfId}, " +
                        $"but the skin only contains {node.Skin.JointsCount} joints.");
                }

                var (jointNode, ibm) = node.Skin.GetJoint(gltfId);

                if (!objectsByNode.TryGetValue(jointNode, out var bone))
                {
                    throw new Exception(
                        $"GLTF joint {gltfId} \"{jointNode.Name}\" " +
                        $"was not imported as an Object3D.");
                }

                ushort modelId = (ushort)skinBones.Objects.Count;

                ibm.Translation *= scaleFactor;

                skinBones.rotations.Add(ibm);
                skinBones.Objects.Add(bone);
                bmi.bones.Add(modelId);
            }

            skinBones.bone_mappings.Add(bmi);

            foreach (var ra in model.RenderAtoms)
                skinBones.bone_mappings.Add(bmi);

            data.AddSection(skinBones);
            model.SkinBones = skinBones;
        }

        private void RemapBoneIds(GLTF.Skin src, DM.Model model)
        {
            DM.SkinBones skinBones = model.SkinBones;

            Dictionary<DM.Object3D, ushort> sbIds = new Dictionary<DM.Object3D, ushort>();
            for (ushort sbId = 0; sbId < skinBones.count; sbId++)
            {
                DM.Object3D bone = skinBones.Objects[sbId];
                sbIds[bone] = sbId;
            }

            ushort? LookupNewBoneId(DM.Object3D bone)
            {
                ushort sbId;
                bool found = sbIds.TryGetValue(bone, out sbId);
                if (found)
                    return sbId;

                return bone.Parent != null ? LookupNewBoneId(bone.Parent) : null;
            }

            Dictionary<ushort, ushort> idMapping = new Dictionary<ushort, ushort>();
            for (ushort gltfId = 0; gltfId < src.JointsCount; gltfId++)
            {
                (GLTF.Node jointNode, _) = src.GetJoint(gltfId);

                if (!objectsByNode.TryGetValue(jointNode, out var obj))
                {
                    throw new Exception(
                        $"GLTF joint {gltfId} \"{jointNode.Name}\" " +
                        $"was not imported as an Object3D.");
                }

                ushort? id = LookupNewBoneId(obj);
                idMapping[gltfId] = id ?? 0;
            }

            DM.Geometry geom = model.PassthroughGP.Geometry;
            for (int i = 0; i < geom.vert_count; i++)
            {
                DM.GeometryWeightGroups group = geom.weight_groups[i];
                ushort id1 = idMapping[group.Bones1];
                ushort id2 = idMapping[group.Bones2];
                ushort id3 = idMapping[group.Bones3];
                ushort id4 = idMapping[group.Bones4];
                geom.weight_groups[i] = new DM.GeometryWeightGroups(id1, id2, id3, id4);
            }
        }

        public class MeshSections
        {
            public DM.Geometry geom;
            public DM.Topology topo;
            public DM.TopologyIP topoip;
            public DM.PassthroughGP passgp;
            public List<DM.RenderAtom> atoms = new List<DM.RenderAtom>();

            public void PopulateFromMeshData(MeshData md)
            {
                geom.Headers.Clear();

                void AddToGeom<TD>(ref List<TD> dest, uint size, DM.GeometryChannelTypes ct, IList<TD> src)
                {
                    if (src.Count > 0)
                    {
                        geom.Headers.Add(new DM.GeometryHeader(size, ct));
                        dest = src.ToList();
                    }
                }

                AddToGeom(ref geom.verts, 3, DM.GeometryChannelTypes.POSITION0, md.verts);
                AddToGeom(ref geom.normals, 8, DM.GeometryChannelTypes.NORMAL0, md.normals);
                AddToGeom(ref geom.binormals, 8, DM.GeometryChannelTypes.BINORMAL0, md.binormals);
                AddToGeom(ref geom.tangents, 8, DM.GeometryChannelTypes.TANGENT0, md.tangents);
                AddToGeom(ref geom.vertex_colors, 5, DM.GeometryChannelTypes.COLOR0, md.vertex_colors);

                for (var i = 0; i < md.uv0.Length; i++)
                {
                    var ct = (DM.GeometryChannelTypes)
                        ((int)DM.GeometryChannelTypes.TEXCOORD0 + i);

                    AddToGeom(ref geom.UVs[i], 9, ct, md.uv0[i]);
                }

                if (md.weights.Count > 0 && md.weights.Count != md.verts.Count)
                {
                    throw new Exception(
                        $"Mesh has {md.verts.Count} vertices but {md.weights.Count} weights.");
                }

                if (md.weightGroups.Count > 0 && md.weightGroups.Count != md.verts.Count)
                {
                    throw new Exception(
                        $"Mesh has {md.verts.Count} vertices but {md.weightGroups.Count} weight groups.");
                }

                if (md.weights.Count > 0 && md.weightGroups.Count == 0)
                {
                    throw new Exception("Mesh has weights but no weight groups.");
                }

                if (md.weightGroups.Count > 0 && md.weights.Count == 0)
                {
                    throw new Exception("Mesh has weight groups but no weights.");
                }

                AddToGeom(ref geom.weights, 3, DM.GeometryChannelTypes.BLENDWEIGHT0, md.weights);
                AddToGeom(ref geom.weight_groups, 7, DM.GeometryChannelTypes.BLENDINDICES0, md.weightGroups);

                geom.vert_count = (uint)geom.verts.Count;
                topo.facelist = md.faces;
            }

            public void Scale(float fac)
            {
                for (var i = 0; i < geom.verts.Count; i++)
                {
                    geom.verts[i] = geom.verts[i] * fac;
                }
            }
        }

        public class MeshData
        {
            public List<Vector3> verts = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            public List<DM.GeometryColor> vertex_colors = new List<DM.GeometryColor>();
            public List<Vector3> binormals = new List<Vector3>();
            public List<Vector3> tangents = new List<Vector3>();
            public List<DM.Face> faces = new List<DM.Face>();
            public List<DM.RenderAtom> renderAtoms = new List<DM.RenderAtom>();
            public List<string> materials = new List<string>();
            public List<Vector2>[] uv0 = new List<Vector2>[] {
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
            };
            public List<Vector3> weights = new List<Vector3>();
            public List<DM.GeometryWeightGroups> weightGroups = new List<DM.GeometryWeightGroups>();

            public int AppendVertex(Vertex vtx)
            {
                var idx = this.verts.Count;
                this.verts.Add(vtx.pos);
                vtx.vtx_col.WithValue(v => this.vertex_colors.Add(v.ToGeometryColor()));
                vtx.normal.WithValue(v => this.normals.Add(v));
                vtx.tangent.WithValue(v => this.tangents.Add(v));
                vtx.binormal.WithValue(v => this.binormals.Add(v));
                for (var i = 0; i < 8; i++)
                {
                    vtx.uv[i].WithValue(v => this.uv0[i].Add(v));
                }
                vtx.weight.WithValue(v => this.weights.Add(v));
                if (vtx.weightGroups != null)
                {
                    this.weightGroups.Add(vtx.weightGroups);
                }
                return idx;
            }

            public void AppendVertices(IEnumerable<Vertex> vertices)
            {
                foreach (var i in vertices)
                {
                    AppendVertex(i);
                }
            }

            public static MeshData FromGltfMesh(GLTF.Mesh mesh)
            {
                var vcount = mesh.Primitives.Select(prim => prim.VertexAccessors["POSITION"].Count).Sum();
                if (vcount >= ushort.MaxValue)
                {
                    throw new Exception($"Too many vertices ({vcount}) in mesh {mesh.Name}. Limit is 65535");
                }

                var attribsUsed = mesh.Primitives.First().VertexAccessors.Select(i => i.Key).OrderBy(i => i);
                var ok = mesh.Primitives.Select(i => i.VertexAccessors.Keys.OrderBy(j => j)).Aggregate(true, (acc, curr) => acc && curr.SequenceEqual(attribsUsed));
                if (!ok)
                {
                    throw new Exception("Vertex attributes not consistent between Primitives. Diesel cannot represent this.");
                }

                var ms = new MeshData();
                ms.materials = mesh.Primitives.Select(i => i.Material?.Name ?? "Material: Default Material").Distinct().ToList();

                uint currentBaseVertex = 0;
                uint currentBaseIndex = 0;

                foreach (var prim in mesh.Primitives)
                {
                    var vertices = GetVerticesFromPrimitive(prim).ToList();
                    var primFaces = prim.GetTriangleIndices().ToList();
                    var matname = prim.Material?.Name ?? "Material: Default Material";

                    var ra = new DM.RenderAtom
                    {
                        BaseIndex = currentBaseIndex,
                        BaseVertex = currentBaseVertex,
                        MaterialId = (uint)ms.materials.IndexOf(matname),
                        TriangleCount = (uint)primFaces.Count
                    };

                    var vertexIds = new Dictionary<Vertex, int>();
                    foreach (var (A, B, C) in primFaces)
                    {
                        var vtxA = vertices[A];
                        var vtxB = vertices[B];
                        var vtxC = vertices[C];

                        if (!vertexIds.ContainsKey(vtxA))
                        {
                            vertexIds[vtxA] = ms.AppendVertex(vtxA);
                        }
                        if (!vertexIds.ContainsKey(vtxB))
                        {
                            vertexIds[vtxB] = ms.AppendVertex(vtxB);
                        }
                        if (!vertexIds.ContainsKey(vtxC))
                        {
                            vertexIds[vtxC] = ms.AppendVertex(vtxC);
                        }

                        var df = new DM.Face(
                            (ushort)vertexIds[vtxA],
                            (ushort)vertexIds[vtxB],
                            (ushort)vertexIds[vtxC]
                        );

                        ms.faces.Add(df);
                    }

                    ra.GeometrySliceLength = (uint)vertexIds.Count;
                    ms.renderAtoms.Add(ra);

                    currentBaseIndex += ra.TriangleCount * 3;
                    currentBaseVertex += ra.GeometrySliceLength;
                }

                return ms;
            }

            static IEnumerable<Vertex> GetVerticesFromPrimitive(GLTF.MeshPrimitive prim)
            {
                var pos = prim.VertexAccessors["POSITION"];
                var result = pos.AsVector3Array().Select((p, idx) =>
                {
                    return new Vertex { pos = p };
                });

                prim.VertexAccessors.TryGetValue("NORMAL", out var normal);
                if (normal != null && normal.Count > 0)
                {
                    var na = normal.AsVector3Array();
                    result = result.Select((vtx, idx) =>
                    {
                        vtx.normal = na[idx];
                        return vtx;
                    });
                }

                prim.VertexAccessors.TryGetValue("TANGENT", out var tangent);
                if (tangent != null && tangent.Count > 0)
                {
                    var ta = tangent.AsVector4Array();
                    result = result.Select((vtx, idx) =>
                    {
                        var et = ta[idx];
                        var tangent_vector = new Vector3(et.X, et.Y, et.Z);
                        var binormal = Vector3.Cross(tangent_vector, vtx.normal.Value) * et.W;
                        vtx.tangent = tangent_vector;
                        vtx.binormal = binormal;
                        return vtx;
                    });
                }

                prim.VertexAccessors.TryGetValue("COLOR_0", out var vcols);
                if (vcols != null && vcols.Count > 0)
                {
                    if (vcols.Dimensions == GLTF.DimensionType.VEC4)
                    {
                        var vca = vcols.AsVector4Array();
                        result = result.Select((vtx, idx) =>
                        {
                            vtx.vtx_col = vca[idx];
                            return vtx;
                        });
                    }
                    else
                    {
                        var vca = vcols.AsVector3Array();
                        result = result.Select((vtx, idx) =>
                        {
                            vtx.vtx_col = new Vector4(vca[idx], 1.0f);
                            return vtx;
                        });
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    var ii = i;
                    prim.VertexAccessors.TryGetValue($"TEXCOORD_{ii}", out var uv0);
                    if (uv0 != null && uv0.Count > 0)
                    {
                        var uva = uv0.AsVector2Array();
                        result = result.Select((vtx, idx) =>
                        {
                            vtx.uv[ii] = new Vector2(uva[idx].X, 1 - uva[idx].Y);
                            return vtx;
                        });
                    }
                }

                if ((tangent == null || tangent.Count == 0) &&
                    prim.VertexAccessors.ContainsKey("POSITION") &&
                    prim.VertexAccessors.ContainsKey("NORMAL") &&
                    prim.VertexAccessors.Keys.Any(k => k.StartsWith("TEXCOORD_")))
                {
                    try
                    {
                        var toolkitAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "SharpGLTF.Toolkit");
                        if (toolkitAssembly != null)
                        {
                            var extensionsType = toolkitAssembly.GetType("SharpGLTF.Toolkit.Extensions");
                            var createMesh = extensionsType?.GetMethod("CreateMesh", BindingFlags.Public | BindingFlags.Static);
                            var tkMesh = createMesh?.Invoke(null, new object[] { prim });

                            var genType = toolkitAssembly.GetType("SharpGLTF.Toolkit.TangentSpaceGenerator");
                            var genMethod = genType?.GetMethod("GenerateTangents", BindingFlags.Public | BindingFlags.Static);
                            genMethod?.Invoke(null, new object[] { tkMesh });

                            prim.VertexAccessors.TryGetValue("TANGENT", out tangent);
                        }
                    }
                    catch { }

                    var positions = prim.VertexAccessors["POSITION"].AsVector3Array().ToArray();
                    var normalsArr = prim.VertexAccessors["NORMAL"].AsVector3Array().ToArray();
                    string uvKey = prim.VertexAccessors.Keys.First(k => k.StartsWith("TEXCOORD_"));
                    var uvs = prim.VertexAccessors[uvKey].AsVector2Array().Select(u => new Vector2(u.X, u.Y)).ToArray();
                    var tris = prim.GetTriangleIndices().ToList();

                    if (positions.Length != 0 && normalsArr.Length != 0 && uvs.Length != 0 && tris.Count != 0)
                    {
                        int vn = positions.Length;
                        var tan1 = new Vector3[vn];
                        var tan2 = new Vector3[vn];

                        for (int i = 0; i < tris.Count; i++)
                        {
                            var t = tris[i];
                            int i1 = t.A, i2 = t.B, i3 = t.C;
                            var v1 = positions[i1];
                            var v2 = positions[i2];
                            var v3 = positions[i3];
                            var w1 = uvs[i1];
                            var w2 = uvs[i2];
                            var w3 = uvs[i3];
                            var x1 = v2.X - v1.X;
                            var x2 = v3.X - v1.X;
                            var y1 = v2.Y - v1.Y;
                            var y2 = v3.Y - v1.Y;
                            var z1 = v2.Z - v1.Z;
                            var z2 = v3.Z - v1.Z;
                            var s1 = w2.X - w1.X;
                            var s2 = w3.X - w1.X;
                            var t1 = w2.Y - w1.Y;
                            var t2 = w3.Y - w1.Y;
                            float denom = (s1 * t2 - s2 * t1);
                            if (Math.Abs(denom) < 1e-9f) continue;
                            float r = 1.0f / denom;
                            var sdir = new Vector3(
                                (t2 * x1 - t1 * x2) * r,
                                (t2 * y1 - t1 * y2) * r,
                                (t2 * z1 - t1 * z2) * r);
                            var tdir = new Vector3(
                                (s1 * x2 - s2 * x1) * r,
                                (s1 * y2 - s2 * y1) * r,
                                (s1 * z2 - s2 * z1) * r);
                            tan1[i1] += sdir;
                            tan1[i2] += sdir;
                            tan1[i3] += sdir;
                            tan2[i1] += tdir;
                            tan2[i2] += tdir;
                            tan2[i3] += tdir;
                        }

                        var outT = new Vector4[vn];
                        for (int i = 0; i < vn; i++)
                        {
                            var nrm = normalsArr[i];
                            var t = tan1[i];
                            var orth = t - nrm * Vector3.Dot(nrm, t);
                            if (orth.LengthSquared() < 1e-18f) orth = Vector3.UnitX;
                            orth = Vector3.Normalize(orth);
                            var cross = Vector3.Cross(nrm, t);
                            float w = Vector3.Dot(cross, tan2[i]) < 0.0f ? -1.0f : 1.0f;
                            outT[i] = new Vector4(orth, w);
                        }

                        result = result.Select((vtx, idx) =>
                        {
                            vtx.tangent = new Vector3(outT[idx].X, outT[idx].Y, outT[idx].Z);
                            vtx.binormal = Vector3.Cross(vtx.tangent.Value, vtx.normal.Value) * outT[idx].W;
                            return vtx;
                        });
                    }
                }

                prim.VertexAccessors.TryGetValue("JOINTS_0", out var joints);
                prim.VertexAccessors.TryGetValue("WEIGHTS_0", out var weights);

                if (joints != null && joints.Count > 0)
                {
                    if (weights == null || weights.Count != joints.Count)
                    {
                        throw new Exception(
                            $"{prim.LogicalParent.Name} has JOINTS_0 without matching WEIGHTS_0.");
                    }

                    var ja = joints.AsVector4Array();
                    var wa = weights.AsVector4Array();

                    result = result.Select((vtx, idx) =>
                    {
                        var gltfWeight = wa[idx];

                        vtx.weight = new Vector3(
                            gltfWeight.X,
                            gltfWeight.Y,
                            gltfWeight.Z);

                        if (gltfWeight.W > 0.00001f)
                        {
                            Log.Default.Warn(
                                $"{prim.LogicalParent.Name} has a vertex with a fourth " +
                                $"non-zero weight at {vtx.pos}; Diesel only supports three.");
                        }

                        vtx.weightGroups = new DM.GeometryWeightGroups(
                            (ushort)ja[idx].X,
                            (ushort)ja[idx].Y,
                            (ushort)ja[idx].Z,
                            (ushort)ja[idx].W);

                        return vtx;
                    });
                }
                else if (weights != null && weights.Count > 0)
                {
                    throw new Exception(
                        $"{prim.LogicalParent.Name} has WEIGHTS_0 without JOINTS_0.");
                }

                return result;
            }
        }

        public class Vertex : IEquatable<Vertex>
        {
            public Vector3 pos;
            public Vector3? normal, tangent, binormal;
            public Vector4? vtx_col;
            public Vector2?[] uv = new Vector2?[8];
            public Vector3? weight;
            public DM.GeometryWeightGroups weightGroups;

            public override bool Equals(object obj)
            {
                return Equals(obj as Vertex);
            }

            public bool Equals(Vertex other)
            {
                return other != null &&
                       pos.Equals(other.pos) &&
                       EqualityComparer<Vector3?>.Default.Equals(normal, other.normal) &&
                       EqualityComparer<Vector3?>.Default.Equals(tangent, other.tangent) &&
                       EqualityComparer<Vector3?>.Default.Equals(binormal, other.binormal) &&
                       EqualityComparer<Vector4?>.Default.Equals(vtx_col, other.vtx_col) &&
                       EqualityComparer<Vector2?[]>.Default.Equals(uv, other.uv) &&
                       EqualityComparer<Vector3?>.Default.Equals(weight, other.weight) &&
                       EqualityComparer<DM.GeometryWeightGroups>.Default.Equals(weightGroups, other.weightGroups);
            }

            public override int GetHashCode()
            {
                var hashCode = -1990297534;
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3>.Default.GetHashCode(pos);
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3?>.Default.GetHashCode(normal);
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3?>.Default.GetHashCode(tangent);
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3?>.Default.GetHashCode(binormal);
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector4?>.Default.GetHashCode(vtx_col);
                for (int i = 0; i < uv.Length; i++)
                {
                    uv[i].WithValue(v => hashCode = hashCode * -1521134295 + EqualityComparer<Vector2>.Default.GetHashCode(v));
                }
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3?>.Default.GetHashCode(weight);
                hashCode = hashCode * -1521134295 + EqualityComparer<DM.GeometryWeightGroups>.Default.GetHashCode(weightGroups);
                return hashCode;
            }
        }

        private void ImportAnimations(GLTF.ModelRoot root)
        {
            foreach (var anim in root.LogicalAnimations)
            {
                foreach (var chan in anim.Channels)
                {
                    var node = chan.TargetNode;
                    var targetObject = objectsByNode[node];
                    if (chan.TargetNodePath == GLTF.PropertyPath.rotation)
                    {
                        var sampler = chan.GetRotationSampler();
                        AddRotationAnimation(targetObject, sampler);
                    }
                    else if (chan.TargetNodePath == GLTF.PropertyPath.translation)
                    {
                        var sampler = chan.GetTranslationSampler();
                        AddTranslationAnimation(targetObject, sampler);
                    }
                }
            }
        }

        private void AddRotationAnimation(DM.Object3D targetObject, GLTF.IAnimationSampler<Quaternion> sampler)
        {
            var controller = new DM.QuatLinearRotationController();
            foreach (var (key, value) in sampler.GetLinearKeys())
            {
                controller.Keyframes.Add(new DM.Keyframe<Quaternion>(key, value));
            }
            controller.KeyframeLength = controller.Keyframes.Select(i => i.Timestamp).Max();

            if (targetObject.Animations.Count == 0)
            {
                targetObject.Animations.Add(controller);
                targetObject.Animations.Add(null);
                targetObject.Animations.Add(null);
            }
            else if (targetObject.Animations.Count == 1 && (targetObject.Animations[0].GetType() == typeof(DM.LinearVector3Controller)))
            {
                targetObject.Animations.Insert(0, controller);
            }
            else
            {
                throw new Exception($"Failed to insert animation in {targetObject.Name}: unrecognised controller list shape");
            }
            data.AddSection(controller);
        }

        private void AddTranslationAnimation(DM.Object3D target, GLTF.IAnimationSampler<Vector3> sampler)
        {
            var controller = new DM.LinearVector3Controller();
            foreach (var (ts, v) in sampler.GetLinearKeys())
            {
                controller.Keyframes.Add(new DM.Keyframe<Vector3>(ts, v * scaleFactor));
            }
            controller.KeyframeLength = controller.Keyframes.Select(i => i.Timestamp).Max();

            if (target.Animations.Count == 0)
            {
                target.Animations.Add(controller);
            }
            else if (target.Animations.Count == 3
                && target.Animations[0].GetType() == typeof(DM.QuatLinearRotationController)
                && target.Animations[1] == null
                && target.Animations[2] == null)
            {
                target.Animations.RemoveAt(2);
                target.Animations[1] = controller;
            }
            else
            {
                throw new Exception($"Failed to insert animation in {target.Name}: unrecognised controller list shape");
            }
            data.AddSection(controller);
        }
    }
}