﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityExplorer.Core.Config;
using UnityExplorer.Core.Input;
using UnityExplorer.Core.Runtime;
using UnityExplorer.UI.Main;
using UnityExplorer.UI.Main.Home;
using UnityExplorer.UI.Utility;

namespace UnityExplorer.UI
{
    public static class UIManager
    {
        public static GameObject CanvasRoot { get; private set; }
        public static EventSystem EventSys { get; private set; }

        internal static Font ConsoleFont { get; private set; }
        internal static Shader BackupShader { get; private set; }

        public static bool ShowMenu
        {
            get => s_showMenu;
            set 
            {
                if (s_showMenu == value || !CanvasRoot)
                    return;

                s_showMenu = value;
                CanvasRoot.SetActive(value);
                CursorUnlocker.UpdateCursorControl();
            }
        }
        public static bool s_showMenu = true;

        internal static void Init()
        {
            CreateRootCanvas();

            LoadBundle();

            UIFactory.Init();

            MainMenu.Create();
            InspectUnderMouse.ConstructUI();
            PanelDragger.CreateCursorUI();

            // Force refresh of anchors etc
            Canvas.ForceUpdateCanvases();

            ShowMenu = !ConfigManager.Hide_On_Startup.Value;

            ExplorerCore.Log("UI initialized.");
        }

        internal static void Update()
        {
            if (!CanvasRoot)
                return;

            if (InspectUnderMouse.Inspecting)
            {
                InspectUnderMouse.UpdateInspect();
                return;
            }

            if (InputManager.GetKeyDown(ConfigManager.Main_Menu_Toggle.Value))
                ShowMenu = !ShowMenu;

            if (!ShowMenu)
                return;

            MainMenu.Instance.Update();

            if (EventSystem.current != EventSys)
                CursorUnlocker.SetEventSystem();

            PanelDragger.Instance.Update();

            SliderScrollbar.UpdateInstances();
            InputFieldScroller.UpdateInstances();
        }

        private static void CreateRootCanvas()
        {
            CanvasRoot = new GameObject("ExplorerCanvas");
            UnityEngine.Object.DontDestroyOnLoad(CanvasRoot);
            CanvasRoot.hideFlags |= HideFlags.HideAndDontSave;
            CanvasRoot.layer = 5;
            CanvasRoot.transform.position = new Vector3(0f, 0f, 1f);

            EventSys = CanvasRoot.AddComponent<EventSystem>();
            InputManager.AddUIModule();

            Canvas canvas = CanvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.referencePixelsPerUnit = 100;
            canvas.sortingOrder = 999;

            CanvasScaler scaler = CanvasRoot.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            CanvasRoot.AddComponent<GraphicRaycaster>();
        }

        private static void LoadBundle()
        {
            AssetBundle bundle = null;

            try
            {
                bundle = LoadExplorerUi("modern");

                if (bundle == null)
                    bundle = LoadExplorerUi("legacy");
            }
            catch
            {
                // ignored
            }

            if (bundle == null)
            {
                ExplorerCore.LogWarning("Could not load the ExplorerUI Bundle!");
                ConsoleFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return;
            }

            BackupShader = bundle.LoadAsset<Shader>("DefaultUI");

            // Fix for games which don't ship with 'UI/Default' shader.
            if (Graphic.defaultGraphicMaterial.shader?.name != "UI/Default")
            {
                ExplorerCore.Log("This game does not ship with the 'UI/Default' shader, using manual Default Shader...");
                Graphic.defaultGraphicMaterial.shader = BackupShader;
            }
            else
                BackupShader = Graphic.defaultGraphicMaterial.shader;

            ConsoleFont = bundle.LoadAsset<Font>("CONSOLA");

            ExplorerCore.Log("Loaded UI AssetBundle");
        }

        private static AssetBundle LoadExplorerUi(string id)
        {
            var stream = typeof(ExplorerCore)
                .Assembly
                .GetManifestResourceStream($"UnityExplorer.Resources.explorerui.{id}.bundle");

            var data = ReadFully(stream);

            return AssetBundle.LoadFromMemory(data);
        }

        private static byte[] ReadFully(Stream input)
        {
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, read);
                return ms.ToArray();
            }
        }
    }
}
