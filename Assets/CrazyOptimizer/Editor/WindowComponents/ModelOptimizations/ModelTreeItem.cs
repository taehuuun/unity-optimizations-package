using CrazyGames.TreeLib;
using System;
using System.IO;
using UnityEditor;

namespace CrazyGames.WindowComponents.ModelOptimizations
{
    public class ModelTreeItem : TreeElement
    {
        public string ModelPath { get; }
        public string ModelName { get; }

        public bool IsReadWriteEnabled => _modelImporter.isReadable;
        public bool ArePolygonsOptimized => _modelImporter.optimizeMeshPolygons;
        public bool AreVerticesOptimized => _modelImporter.optimizeMeshVertices;
        public ModelImporterMeshCompression MeshCompression => _modelImporter.meshCompression;
        public ModelImporterAnimationCompression AnimationCompression => _modelImporter.animationCompression;

        public string MeshCompressionName
        {
            get
            {
                switch (MeshCompression)
                {
                    case ModelImporterMeshCompression.Off:
                        return "압축 안함";
                    case ModelImporterMeshCompression.Low:
                        return "낮음";
                    case ModelImporterMeshCompression.Medium:
                        return "일반";
                    case ModelImporterMeshCompression.High:
                        return "높음";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public string AnimationCompressionName
        {
            get
            {
                switch (AnimationCompression)
                {
                    case ModelImporterAnimationCompression.Off:
                        return "압축 안함";
                    case ModelImporterAnimationCompression.KeyframeReduction:
                        return "키프레임 감소";
                    case ModelImporterAnimationCompression.KeyframeReductionAndCompression:
                        return "키프레임 감소 및 압축";
                    case ModelImporterAnimationCompression.Optimal:
                        return "최적";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private readonly ModelImporter _modelImporter;

        public ModelTreeItem(string name, int depth, int id, string modelPath, ModelImporter modelImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            ModelPath = modelPath;
            ModelName = Path.GetFileName(modelPath);

            _modelImporter = modelImporter;
        }
    }
}