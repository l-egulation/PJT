using System;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public static class LocomotionSource
    {
        public static LocomotionData Load(string path, out string fileHash)
        {
            var data = JsonUtility.FromJson<LocomotionData>(SourceFiles.Read(path, out fileHash));
            Validate(data);
            return data;
        }

        public static LocomotionProfile Validate(LocomotionData data)
        {
            if (!LocomotionProfile.TryCreate(data, out var profile, out var error))
                throw new ArgumentException(error);
            return profile;
        }

        public static string Save(string path, string expectedFileHash, LocomotionData draft) =>
            SourceFiles.Save(path, expectedFileHash, JsonUtility.ToJson(Validate(draft).ToData(), true) + "\n");

        public static void Export(string sourcePath, string expectedFileHash, string generatedPath)
        {
            Load(sourcePath, out _);
            SourceFiles.Copy(sourcePath, expectedFileHash, generatedPath);
        }
    }
}
