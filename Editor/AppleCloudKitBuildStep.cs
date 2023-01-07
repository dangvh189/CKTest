using Apple.Core;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
#endif

namespace Apple.CloudKit.Editor
{
    public class AppleCloudKitBuildStep : AppleBuildStep
    {
        public override string DisplayName => "CloudKit";

        public bool UseiCloudContainers = false;
        public List<string> iCloudContainerIdentifiers = new List<string>();
        public CloudKitContainerEnvironment iCloudContainerEnvironment = CloudKitContainerEnvironment.Auto;
        public bool UseUbiquityKeyValueStore = false;
        public string UbiquityKeyValueStoreIdentifier = "$(TeamIdentifierPrefix).$(CFBundleIdentifier)";
        
        [Tooltip("Adds the aps-environment entitlement, which may be required on some platforms when publishing to TestFlight.")]
        public bool UseAPS = false;
        public APSEnvironment APSEnvironment = APSEnvironment.Auto;
        readonly Dictionary<BuildTarget, string> _libraryTable = new Dictionary<BuildTarget, string>
        {
            {BuildTarget.iOS, "CloudKitWrapper.framework"},
            {BuildTarget.tvOS, "CloudKitWrapper.framework"},
            {BuildTarget.StandaloneOSX, "CloudKitWrapper.bundle"}
        };
        
#if UNITY_EDITOR_OSX
        public override void OnProcessFrameworks(AppleBuildProfile _, BuildTarget buildTarget, string pathToBuiltTarget, PBXProject pbxProject)
        {
            if (_libraryTable.ContainsKey(buildTarget))
            {
                string libraryName = _libraryTable[buildTarget];
                string libraryPath = AppleFrameworkUtility.GetPluginLibraryPathForBuildTarget(libraryName, buildTarget);
                if (String.IsNullOrEmpty(libraryPath))
                {
                    Debug.Log($"Failed to locate path for library: {libraryName}");
                }
                else
                {
                    AppleFrameworkUtility.CopyAndEmbed(libraryPath, buildTarget, pathToBuiltTarget, pbxProject);
                    AppleFrameworkUtility.AddFrameworkToProject("CloudKit.framework", false, buildTarget, pbxProject);
                }
            }
            else
            {
                Debug.Log($"Processing {this.DisplayName} frameworks for unsupported platform. Skipping.");
            }
        }

        public override void OnProcessEntitlements(AppleBuildProfile appleBuildProfile, BuildTarget buildTarget, string pathToBuiltTarget, PlistDocument entitlements)
        {
            if(UseiCloudContainers || UseUbiquityKeyValueStore)
            {
                var services = entitlements.root.CreateArray("com.apple.developer.icloud-services");
                services.AddString("CloudKit");
            }

            if(UseiCloudContainers)
            {
                var containers = entitlements.root.CreateArray("com.apple.developer.icloud-container-identifiers");

                foreach (var container in iCloudContainerIdentifiers)
                {
                    var safeContainerName = container;

                    // Replace the $(CFBundleIdentifier) for non-xcode generated mac builds.
                    safeContainerName = safeContainerName.Replace("$(CFBundleIdentifier)", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone));

                    containers.AddString(safeContainerName);
                }

                switch(iCloudContainerEnvironment)
                {
                    case CloudKitContainerEnvironment.Auto:
                        entitlements.root.SetString("com.apple.developer.icloud-container-environment", EditorUserBuildSettings.iOSBuildConfigType == iOSBuildType.Debug ? "Development" : "Production");
                        break;
                    default:
                        entitlements.root.SetString("com.apple.developer.icloud-container-environment", iCloudContainerEnvironment.ToString());
                        break;
                }
            }

            if(UseUbiquityKeyValueStore)
            {
                var safeIdentifier = UbiquityKeyValueStoreIdentifier;

                // Replace $(TeamIdentifierPrefix) and $(CFBundleIdentifier).
                safeIdentifier = safeIdentifier.Replace("$(TeamIdentifierPrefix)", PlayerSettings.iOS.appleDeveloperTeamID);
                safeIdentifier = safeIdentifier.Replace("$(CFBundleIdentifier)", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone));

                entitlements.root.SetString("com.apple.developer.ubiquity-kvstore-identifier", safeIdentifier);
            }

            if(UseAPS && buildTarget != BuildTarget.StandaloneOSX)
            {
                switch (APSEnvironment)
                {
                    case APSEnvironment.Auto:
                        entitlements.root.SetString("aps-environment", EditorUserBuildSettings.iOSBuildConfigType == iOSBuildType.Debug ? "Development" : "Production");
                        break;
                    default:
                        entitlements.root.SetString("aps-environment", APSEnvironment.ToString());
                        break;
                }
            }
        }

        public override void OnProcessExportPlistOptions(AppleBuildProfile appleBuildProfile, BuildTarget buildTarget, string pathToBuiltProject, PlistDocument exportPlistOptions)
        {
            if (UseiCloudContainers)
            {
                switch (iCloudContainerEnvironment)
                {
                    case CloudKitContainerEnvironment.Auto:
                        exportPlistOptions.root.SetString("iCloudContainerEnvironment", EditorUserBuildSettings.iOSBuildConfigType == iOSBuildType.Debug ? "Development" : "Production");
                        break;
                    default:
                        exportPlistOptions.root.SetString("iCloudContainerEnvironment", iCloudContainerEnvironment.ToString());
                        break;
                }
            }
        }
#endif
    }
}