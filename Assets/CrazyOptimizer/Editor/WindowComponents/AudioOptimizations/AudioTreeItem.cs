using CrazyGames.TreeLib;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrazyGames.WindowComponents.AudioOptimizations
{
    public class AudioTreeItem : TreeElement
    {
        public string AudioPath { get; }
        public string AudioName { get; }

        public string LoadType
        {
            get
            {
                switch (_platformSettings.loadType)
                {
                    case AudioClipLoadType.DecompressOnLoad:
                        return "로드 시 해제";
                    case AudioClipLoadType.CompressedInMemory:
                        return "메모리 내 압축";
                    case AudioClipLoadType.Streaming:
                        return "스트리밍";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public int Quality => Mathf.RoundToInt(_platformSettings.quality * 100);

        private readonly AudioImporter _audioImporter;
        private readonly AudioImporterSampleSettings _platformSettings;

        public AudioTreeItem(string name, int depth, int id, string audioPath, AudioImporter audioImporter) : base(name, depth, id)
        {
            if (depth == -1)
                return;

            AudioPath = audioPath;
            AudioName = Path.GetFileName(audioPath);

            _audioImporter = audioImporter;
            _platformSettings = _audioImporter.GetOverrideSampleSettings("WebGL");
        }
    }
}