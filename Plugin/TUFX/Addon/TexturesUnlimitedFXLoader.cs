﻿using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace TUFX
{

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class TexturesUnlimitedFXLoader : MonoBehaviour
    {

        public static TexturesUnlimitedFXLoader INSTANCE;
        private static ApplicationLauncherButton configAppButton;
        private ConfigurationGUI configGUI;

        private Dictionary<string, Shader> shaders = new Dictionary<string, Shader>();
        private Dictionary<string, ComputeShader> computeShaders = new Dictionary<string, ComputeShader>();
        private Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        private Dictionary<string, TUFXProfile> profiles = new Dictionary<string, TUFXProfile>();

        /// <summary>
        /// Reference to the Unity Post Processing 'Resources' class.  Used to store references to the shaders and textures used by the post-processing system internals.
        /// Does not include references to the 'included' but 'external' resources such as the built-in lens-dirt textures or any custom LUTs.
        /// </summary>
        public PostProcessResources Resources { get; private set; }

        public void Start()
        {
            MonoBehaviour.print("TUFXLoader - Start()");
            INSTANCE = this;
            DontDestroyOnLoad(this);
            GameEvents.onLevelWasLoaded.Add(new EventData<GameScenes>.OnEvent(onSceneChange));
        }
        
        public void ModuleManagerPostLoad()
        {
            Log.log("TUFXLoader - MMPostLoad()");

            //only load resources once.  In case of MM reload...
            if (Resources == null)
            {
                loadResources();
            }
            profiles.Clear();
            ConfigNode[] profileConfigs = GameDatabase.Instance.GetConfigNodes("TUFX_PROFILE");
            int len = profileConfigs.Length;
            for (int i = 0; i < len; i++)
            {
                TUFXProfile profile = new TUFXProfile(profileConfigs[i]);
                if (!profiles.ContainsKey(profile.ProfileName))
                {
                    profiles.Add(profile.ProfileName, profile);
                }
                else
                {
                    Log.exception("TUFX Profiles already contains profile for name: " + profile.ProfileName + ".  This is the result of a duplicate configuration; please check your configurations and remove any duplicates.");
                }
            }
        }

        private void loadResources()
        {
            Resources = ScriptableObject.CreateInstance<PostProcessResources>();
            Resources.shaders = new PostProcessResources.Shaders();
            Resources.computeShaders = new PostProcessResources.ComputeShaders();
            Resources.blueNoise64 = new Texture2D[64];
            Resources.blueNoise256 = new Texture2D[8];
            Resources.smaaLuts = new PostProcessResources.SMAALuts();

            //previously this did not work... but appears to with these bundles/Unity version
            AssetBundle bundle = AssetBundle.LoadFromFile(KSPUtil.ApplicationRootPath + "GameData/TUFX/Shaders/tufx-universal.ssf");
            Shader[] shaders = bundle.LoadAllAssets<Shader>();
            int len = shaders.Length;
            for (int i = 0; i < len; i++)
            {
                if (!this.shaders.ContainsKey(shaders[i].name)) { this.shaders.Add(shaders[i].name, shaders[i]); }
            }
            ComputeShader[] compShaders = bundle.LoadAllAssets<ComputeShader>();
            len = compShaders.Length;
            for (int i = 0; i < len; i++)
            {
                if (!this.computeShaders.ContainsKey(compShaders[i].name)) { this.computeShaders.Add(compShaders[i].name, compShaders[i]); }
            }
            bundle.Unload(false);

            #region REGION - Load standard Post Process Effect Shaders
            Resources.shaders.bloom = getShader("Hidden/PostProcessing/Bloom");
            Resources.shaders.copy = getShader("Hidden/PostProcessing/Copy");
            Resources.shaders.copyStd = getShader("Hidden/PostProcessing/CopyStd");
            Resources.shaders.copyStdFromDoubleWide = getShader("Hidden/PostProcessing/CopyStdFromDoubleWide");
            Resources.shaders.copyStdFromTexArray = getShader("Hidden/PostProcessing/CopyStdFromTexArray");
            Resources.shaders.deferredFog = getShader("Hidden/PostProcessing/DeferredFog");
            Resources.shaders.depthOfField = getShader("Hidden/PostProcessing/DepthOfField");
            Resources.shaders.discardAlpha = getShader("Hidden/PostProcessing/DiscardAlpha");
            Resources.shaders.finalPass = getShader("Hidden/PostProcessing/FinalPass");
            Resources.shaders.gammaHistogram = getShader("Hidden/PostProcessing/Debug/Histogram");//TODO - part of debug shaders?
            Resources.shaders.grainBaker = getShader("Hidden/PostProcessing/GrainBaker");
            Resources.shaders.lightMeter = getShader("Hidden/PostProcessing/Debug/LightMeter");//TODO - part of debug shaders?
            Resources.shaders.lut2DBaker = getShader("Hidden/PostProcessing/Lut2DBaker");
            Resources.shaders.motionBlur = getShader("Hidden/PostProcessing/MotionBlur");
            Resources.shaders.multiScaleAO = getShader("Hidden/PostProcessing/MultiScaleVO");
            Resources.shaders.scalableAO = getShader("Hidden/PostProcessing/ScalableAO");
            Resources.shaders.screenSpaceReflections = getShader("Hidden/PostProcessing/ScreenSpaceReflections");
            Resources.shaders.subpixelMorphologicalAntialiasing = getShader("Hidden/PostProcessing/SubpixelMorphologicalAntialiasing");
            Resources.shaders.temporalAntialiasing = getShader("Hidden/PostProcessing/TemporalAntialiasing");
            Resources.shaders.texture2dLerp = getShader("Hidden/PostProcessing/Texture2DLerp");
            Resources.shaders.uber = getShader("Hidden/PostProcessing/Uber");
            Resources.shaders.vectorscope = getShader("Hidden/PostProcessing/Debug/Vectorscope");//TODO - part of debug shaders?
            Resources.shaders.waveform = getShader("Hidden/PostProcessing/Debug/Waveform");//TODO - part of debug shaders?
            #endregion

            #region REGION - Load compute shaders
            Resources.computeShaders.autoExposure = getComputeShader("AutoExposure");
            Resources.computeShaders.exposureHistogram = getComputeShader("ExposureHistogram");
            Resources.computeShaders.gammaHistogram = getComputeShader("GammaHistogram");//TODO - part of debug shaders?
            Resources.computeShaders.gaussianDownsample = getComputeShader("GaussianDownsample");
            Resources.computeShaders.lut3DBaker = getComputeShader("Lut3DBaker");
            Resources.computeShaders.multiScaleAODownsample1 = getComputeShader("MultiScaleVODownsample1");
            Resources.computeShaders.multiScaleAODownsample2 = getComputeShader("MultiScaleVODownsample2");
            Resources.computeShaders.multiScaleAORender = getComputeShader("MultiScaleVORender");
            Resources.computeShaders.multiScaleAOUpsample = getComputeShader("MultiScaleVOUpsample");
            Resources.computeShaders.texture3dLerp = getComputeShader("Texture3DLerp");
            Resources.computeShaders.vectorscope = getComputeShader("Vectorscope");//TODO - part of debug shaders?
            Resources.computeShaders.waveform = getComputeShader("Waveform");//TODO - part of debug shaders?
            #endregion

            #region REGION - Load textures
            bundle = AssetBundle.LoadFromFile(KSPUtil.ApplicationRootPath + "GameData/TUFX/Textures/tufx-tex-bluenoise64.ssf");
            Texture2D[] tex = bundle.LoadAllAssets<Texture2D>();
            len = tex.Length;
            for (int i = 0; i < len; i++)
            {
                string idxStr = tex[i].name.Substring(tex[i].name.Length - 2).Replace("_", "");
                int idx = int.Parse(idxStr);
                Resources.blueNoise64[idx] = tex[i];
            }
            bundle.Unload(false);

            bundle = AssetBundle.LoadFromFile(KSPUtil.ApplicationRootPath + "GameData/TUFX/Textures/tufx-tex-bluenoise256.ssf");
            tex = bundle.LoadAllAssets<Texture2D>();
            len = tex.Length;
            for (int i = 0; i < len; i++)
            {
                string idxStr = tex[i].name.Substring(tex[i].name.Length - 2).Replace("_", "");
                int idx = int.Parse(idxStr);
                Resources.blueNoise256[idx] = tex[i];
            }
            bundle.Unload(false);

            bundle = AssetBundle.LoadFromFile(KSPUtil.ApplicationRootPath + "GameData/TUFX/Textures/tufx-tex-lensdirt.ssf");
            tex = bundle.LoadAllAssets<Texture2D>();
            len = tex.Length;
            for (int i = 0; i < len; i++)
            {
                textures.Add(tex[i].name, tex[i]);
            }
            bundle.Unload(false);

            bundle = AssetBundle.LoadFromFile(KSPUtil.ApplicationRootPath + "GameData/TUFX/Textures/tufx-tex-smaa.ssf");
            tex = bundle.LoadAllAssets<Texture2D>();
            len = tex.Length;
            for (int i = 0; i < len; i++)
            {
                if (tex[i].name == "AreaTex") { Resources.smaaLuts.area = tex[i]; }
                else { Resources.smaaLuts.search = tex[i]; }
            }
            bundle.Unload(false);
            #endregion
        }

        private Shader getShader(string name)
        {
            shaders.TryGetValue(name, out Shader s);
            return s;
        }

        private ComputeShader getComputeShader(string name)
        {
            computeShaders.TryGetValue(name, out ComputeShader s);
            return s;
        }

        public Texture2D getTexture(string name)
        {
            textures.TryGetValue(name, out Texture2D tex);
            return tex;
        }

        public bool isBuiltinTexture(Texture2D tex)
        {
            return textures.Values.Contains(tex);
        }

        private void onSceneChange(GameScenes scene)
        {
            Log.debug("TUFXLoader - onSceneChange()");

            Log.debug("TUFX - Updating AppLauncher button...");
            if (scene == GameScenes.FLIGHT || scene == GameScenes.SPACECENTER || scene == GameScenes.EDITOR)
            {
                Texture2D tex;
                if (configAppButton == null)//static reference; track if the button was EVER created, as KSP keeps them even if the addon is destroyed
                {
                    //create a new button
                    tex = GameDatabase.Instance.GetTexture("Squad/PartList/SimpleIcons/RDIcon_fuelSystems-highPerformance", false);
                    configAppButton = ApplicationLauncher.Instance.AddModApplication(configGuiEnable, configGuiDisable, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB, tex);
                }
                else if (configAppButton != null)
                {
                    //reseat callback refs to the ones from THIS instance of the KSPAddon (old refs were stale, pointing to methods for a deleted class instance)
                    configAppButton.onTrue = configGuiEnable;
                    configAppButton.onFalse = configGuiDisable;
                }
            }

            string profileName = string.Empty;
            switch (scene)
            {
                case GameScenes.LOADING:
                    break;
                case GameScenes.LOADINGBUFFER:
                    break;
                case GameScenes.MAINMENU:
                    break;
                case GameScenes.SETTINGS:
                    break;
                case GameScenes.CREDITS:
                    break;
                case GameScenes.SPACECENTER:
                    profileName = HighLogic.CurrentGame.Parameters.CustomParams<TUFXGameSettings>().SpaceCenterSceneProfile;
                    break;
                case GameScenes.EDITOR:
                    profileName = HighLogic.CurrentGame.Parameters.CustomParams<TUFXGameSettings>().EditorSceneProfile;
                    break;
                case GameScenes.FLIGHT:
                    profileName = HighLogic.CurrentGame.Parameters.CustomParams<TUFXGameSettings>().FlightSceneProfile;
                    break;
                case GameScenes.TRACKSTATION:
                    break;
                case GameScenes.PSYSTEM:
                    break;
                case GameScenes.MISSIONBUILDER:
                    break;
                default:
                    break;
            }
            Log.debug("TUFX - Enabling profile for current scene: " + scene + " profile: " + profileName);

            if (!string.IsNullOrEmpty(profileName) && profiles.ContainsKey(profileName))
            {
                TUFXProfile profile = profiles[profileName];

                PostProcessLayer layer = Camera.main.gameObject.AddOrGetComponent<PostProcessLayer>();
                layer.Init(Resources);
                layer.volumeLayer = ~0;//everything
                PostProcessVolume volume = Camera.main.gameObject.AddOrGetComponent<PostProcessVolume>();
                volume.isGlobal = true;
                volume.priority = 100;

                PostProcessProfile ppp = ScriptableObject.CreateInstance<PostProcessProfile>();
                volume.sharedProfile = ppp;
                profile.Enable(volume);
            }
            else
            {
                //discard existing data...
            }
        }

        public static void onHDRToggled()
        {
            Log.debug("Toggling HDR");
            Camera[] cams = GameObject.FindObjectsOfType<Camera>();
            int len = cams.Length;
            Log.debug("Found cameras: " + len);
            for (int i = 0; i < len; i++)
            {
                Log.debug("Camera: " + cams[i].name);
                //TODO -- other KSP 3d cameras (not UI)
                if (cams[i].name == "Camera 00" || cams[i].name == "Camera 01")
                {
                    //cams[i].allowHDR = EffectManager.hdrEnabled;
                    //TODO -- re-add HDR effect storage somewhere
                }
            }
        }

        private void configGuiEnable()
        {
            if (configGUI == null)
            {
                configGUI = this.gameObject.AddOrGetComponent<ConfigurationGUI>();
            }
        }

        public void configGuiDisable()
        {
            if (configGUI != null)
            {
                GameObject.Destroy(configGUI);
            }
        }

    }

}
