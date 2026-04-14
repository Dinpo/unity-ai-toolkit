#if ASSET_INVENTORY
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AIToolkit
{
    public static class AISkillInstaller
    {
        private const string SKILLS_SUBFOLDER = "Skills";
        private const string TARGET_FOLDER = ".claude/skills";

        [MenuItem("Tools/AI Toolkit/Install Skills")]
        public static void InstallSkills()
        {
            string packagePath = GetPackagePath();
            if (packagePath == null)
            {
                Debug.LogError("AISkillInstaller: Could not find AI Toolkit package path.");
                return;
            }

            string skillsSource = Path.Combine(packagePath, SKILLS_SUBFOLDER);
            if (!Directory.Exists(skillsSource))
            {
                Debug.LogError($"AISkillInstaller: Skills folder not found at {skillsSource}");
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string targetRoot = Path.Combine(projectRoot, TARGET_FOLDER);
            Directory.CreateDirectory(targetRoot);

            string[] skillDirs = Directory.GetDirectories(skillsSource);
            int created = 0, skipped = 0, warned = 0;

            foreach (string skillDir in skillDirs)
            {
                string skillName = Path.GetFileName(skillDir);
                string targetPath = Path.Combine(targetRoot, skillName);

                if (Directory.Exists(targetPath) || File.Exists(targetPath))
                {
                    if (IsSymlink(targetPath))
                    {
                        skipped++;
                        continue;
                    }

                    Debug.LogWarning($"AISkillInstaller: '{skillName}' already exists as a local skill. Skipping. Delete it manually if you want the toolkit version.");
                    warned++;
                    continue;
                }

                if (CreateSymlink(targetPath, skillDir))
                {
                    created++;
                    Debug.Log($"AISkillInstaller: Linked '{skillName}' -> {skillDir}");
                }
                else
                {
                    warned++;
                    Debug.LogError($"AISkillInstaller: Failed to create symlink for '{skillName}'");
                }
            }

            Debug.Log($"AISkillInstaller: Done. Created: {created}, Skipped: {skipped}, Warnings: {warned}");
        }

        [MenuItem("Tools/AI Toolkit/Uninstall Skills")]
        public static void UninstallSkills()
        {
            string packagePath = GetPackagePath();
            if (packagePath == null)
            {
                Debug.LogError("AISkillInstaller: Could not find AI Toolkit package path.");
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string targetRoot = Path.Combine(projectRoot, TARGET_FOLDER);
            if (!Directory.Exists(targetRoot))
            {
                Debug.Log("AISkillInstaller: No .claude/skills/ folder found. Nothing to uninstall.");
                return;
            }

            string[] entries = Directory.GetDirectories(targetRoot);
            int removed = 0;

            foreach (string entry in entries)
            {
                if (!IsSymlink(entry)) continue;

                string linkTarget = ReadSymlinkTarget(entry);
                if (linkTarget != null && linkTarget.Contains(packagePath))
                {
                    Directory.Delete(entry, false);
                    removed++;
                    Debug.Log($"AISkillInstaller: Removed symlink '{Path.GetFileName(entry)}'");
                }
            }

            Debug.Log($"AISkillInstaller: Uninstalled {removed} skill symlinks.");
        }

        private static string GetPackagePath()
        {
            var assembly = typeof(AISkillInstaller).Assembly;
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            return packageInfo?.resolvedPath;
        }

        private static bool IsSymlink(string path)
        {
            return new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }

        private static bool CreateSymlink(string linkPath, string targetPath)
        {
#if UNITY_EDITOR_WIN
            var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /D \"{linkPath}\" \"{targetPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
#else
            var psi = new ProcessStartInfo("ln", $"-s \"{targetPath}\" \"{linkPath}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
#endif
            var process = Process.Start(psi);
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        private static string ReadSymlinkTarget(string path)
        {
#if UNITY_EDITOR_WIN
            return null;
#else
            var psi = new ProcessStartInfo("readlink", path)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
#endif
        }
    }
}
#endif
