﻿using Battlehub.RTCommon;
using Battlehub.RTHandles;
using UnityEngine;

namespace Battlehub.RTEditor
{
    [DefaultExecutionOrder(-100)]
    public class RTEDeps : MonoBehaviour
    {
        private IRuntimeConsole m_console;
        private IScenePivot m_scenePivot;
        private IResourcePreviewUtility m_resourcePreview;
        private IRTEAppearance m_rteAppearance;
        private IRuntimeEditor m_rte;
        private IWindowManager m_windowManager;
        private IGameObjectCmd m_gameObjectCmd;

        protected virtual IScenePivot ScenePivot
        {
            get { return FindObjectOfType<RuntimeSelectionComponent>(); }
        }

        protected virtual IResourcePreviewUtility ResourcePreview
        {
            get
            {
                IResourcePreviewUtility resourcePreviewUtility = FindObjectOfType<ResourcePreviewUtility>();
                if (resourcePreviewUtility == null)
                {
                    resourcePreviewUtility = gameObject.AddComponent<ResourcePreviewUtility>();
                }
                return resourcePreviewUtility;
            }
        }

        protected virtual IRTEAppearance RTEAppearance
        {
            get
            {
                IRTEAppearance rteAppearance = FindObjectOfType<RTEAppearance>();
                if(rteAppearance == null)
                {
                    rteAppearance = gameObject.AddComponent<RTEAppearance>();
                }
                return rteAppearance;
            }
        }

        protected virtual IRuntimeEditor RTE
        {
            get
            {
                IRuntimeEditor rte = FindObjectOfType<RuntimeEditor>();
                if(rte == null)
                {
                    rte = gameObject.AddComponent<RuntimeEditor>();
                }
                return rte;
            }
        }

        protected virtual IWindowManager WindowManager
        {
            get
            {
                IWindowManager windowManager = FindObjectOfType<WindowManager>();
                if(windowManager == null)
                {
                    windowManager = gameObject.AddComponent<WindowManager>();
                }
                return windowManager;
            }
        }

        protected virtual IRuntimeConsole RuntimeConsole
        {
            get
            {
                IRuntimeConsole console = FindObjectOfType<RuntimeConsole>();
                if(console == null)
                {
                    console = gameObject.AddComponent<RuntimeConsole>();
                }
                return console;
            }
        }

        protected virtual IGameObjectCmd GameObjectCmd
        {
            get
            {
                return FindObjectOfType<GameObjectCmd>();
            }
        }


        private void Awake()
        {
            if (m_instance != null)
            {
                Debug.LogWarning("AnotherInstance of RTSL2 exists");
            }
            m_instance = this;
            AwakeOverride();
        }

        protected virtual void AwakeOverride()
        {
            m_rte = RTE;
            IOC.Register<IRTE>(m_rte);
            IOC.Register(m_rte);

            m_scenePivot = ScenePivot;
            m_resourcePreview = ResourcePreview;
            m_rteAppearance = RTEAppearance;
            m_windowManager = WindowManager;
            m_console = RuntimeConsole;
            m_gameObjectCmd = GameObjectCmd;
        }

        private void OnDestroy()
        {
            if (m_instance == this)
            {
                m_instance = null;
            }

            OnDestroyOverride();

            IOC.Unregister<IRTE>(m_rte);
            IOC.Unregister(m_rte);

            m_resourcePreview = null;
            m_rteAppearance = null;
            m_rte = null;
            m_windowManager = null;
        }

        protected virtual void OnDestroyOverride()
        {

        }

        private static RTEDeps m_instance;
        private static RTEDeps Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindObjectOfType<RTEDeps>();
                    if(m_instance == null)
                    {
                        GameObject go = new GameObject("RTEDeps");
                        go.AddComponent<RTEDeps>();
                    }
                }
                return m_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            IOC.RegisterFallback(() => Instance.m_console);
            IOC.RegisterFallback(() => Instance.m_resourcePreview);
            IOC.RegisterFallback(() => Instance.m_rteAppearance);
            IOC.RegisterFallback(() => Instance.m_scenePivot);
            IOC.RegisterFallback(() => Instance.m_windowManager);
            IOC.RegisterFallback(() => Instance.m_gameObjectCmd);  
        }
    }
}

