﻿using UnityEngine;
using UnityEngine.UI;
using System.Linq;

using Battlehub.UIControls;
using Battlehub.RTCommon;
using Battlehub.UIControls.Dialogs;
using Battlehub.RTSaveLoad2.Interface;
using UnityEngine.SceneManagement;

namespace Battlehub.RTEditor
{
    public class SaveSceneDialog : RuntimeWindow
    {
        [SerializeField]
        private InputField Input = null;
        [SerializeField]
        private Sprite FolderIcon = null;
        [SerializeField]
        private Sprite SceneIcon = null;
    
        private Dialog m_parentDialog;
        private VirtualizingTreeView m_treeView = null;
        private IProject m_project;
        private IWindowManager m_windowManager;

        private void Start()
        {
            m_parentDialog = GetComponentInParent<Dialog>();
            m_parentDialog.Ok += OnOk;
            m_parentDialog.OkText = "Save";
            m_parentDialog.IsOkVisible = true;
            m_parentDialog.CancelText = "Cancel";
            m_parentDialog.IsCancelVisible = true;

            m_treeView = GetComponentInChildren<VirtualizingTreeView>();
            m_windowManager = IOC.Resolve<IWindowManager>();

            m_treeView.ItemDataBinding += OnItemDataBinding;
            m_treeView.ItemExpanding += OnItemExpanding;
            m_treeView.SelectionChanged += OnSelectionChanged;
            m_treeView.ItemDoubleClick += OnItemDoubleClick;
            m_treeView.CanDrag = false;
            m_treeView.CanEdit = false;
            m_treeView.CanUnselectAll = false;
            m_treeView.CanRemove = false;

            m_project = IOC.Resolve<IProject>();
            if (m_project == null)
            {
                Debug.LogError("ProjectManager.Instance is null");
                return;
            }

            m_treeView.Items = new[] { m_project.Root };
            m_treeView.SelectedItem = m_project.Root;
            m_treeView.Expand(m_project.Root);
                      
            Input.ActivateInputField();
        }

        private void OnEnable()
        {
            StartCoroutine(CoActivateInputField());
        }

        private System.Collections.IEnumerator CoActivateInputField()
        {
            yield return new WaitForEndOfFrame();
            if (Input != null)
            {
                Input.ActivateInputField();
            }
        }

        protected override void OnDestroyOverride()
        {
            base.OnDestroyOverride();
          
            if(m_parentDialog != null)
            {
                m_parentDialog.Ok -= OnOk; 
            }

            if (m_treeView != null)
            {
                m_treeView.ItemDataBinding -= OnItemDataBinding;
                m_treeView.ItemExpanding -= OnItemExpanding;
                m_treeView.SelectionChanged -= OnSelectionChanged;
                m_treeView.ItemDoubleClick -= OnItemDoubleClick;
            }
        }


        private void OnOk(Dialog dialog, DialogCancelArgs args)
        {
            if (m_treeView.SelectedItem == null)
            {
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrEmpty(Input.text))
            {
                args.Cancel = true;
                Input.ActivateInputField();
                return;
            }

            if (Input.text != null && Input.text.Length > 0 && (!char.IsLetter(Input.text[0]) || Input.text[0] == '-'))
            {
                m_windowManager.MessageBox("Scene name is invalid", "Scene name should start with letter");
                args.Cancel = true;
                return;
            }

            if (!ProjectItem.IsValidName(Input.text))
            {
                m_windowManager.MessageBox("Scene name is invalid", "Scene name contains invalid characters");
                args.Cancel = true;
                return;
            }

            ProjectItem selectedItem = (ProjectItem)m_treeView.SelectedItem;
            if (IsScene(selectedItem))
            {
                if (Input.text.ToLower() == selectedItem.Name.ToLower())
                {
                    Overwrite(selectedItem);
                    args.Cancel = true;
                }
                else
                {
                    ProjectItem folder = selectedItem.Parent;
                    SaveSceneToFolder(args, folder);
                }
            }
            else
            {
                ProjectItem folder = selectedItem;
                SaveSceneToFolder(args, folder);
            }
        }

        private void Overwrite(ProjectItem selectedItem)
        {
            m_windowManager.Confirmation("Scene with same name already exits", "Do you want to override it?", (sender, yes) =>
            {
                m_parentDialog.gameObject.SetActive(false);
                Editor.Undo.Purge();
                Editor.IsBusy = true;
                m_project.Delete(new[] { selectedItem }, (deleteError, result) =>
                {
                    Editor.IsBusy = false;
                    if (deleteError.HasError)
                    {
                        m_windowManager.MessageBox("Unable to save scene", deleteError.ErrorText);

                    }
                    Editor.IsBusy = true;
                    m_project.Create(m_project.Root, new byte[0], SceneManager.GetActiveScene(), selectedItem.Name, (error, assetItem) =>
                    {
                        Editor.IsBusy = false;
                        if (error.HasError)
                        {
                            m_windowManager.MessageBox("Unable to save scene", error.ErrorText);
                        }
                        else
                        {
                            m_project.LoadedScene = assetItem;
                        }
                        m_parentDialog.Close(null);
                    });
                });
            },
            (sender, no) => Input.ActivateInputField(),
            "Yes",
            "No");
        }

        private bool IsScene(ProjectItem item)
        {
            if(item is AssetItem)
            {
                AssetItem assetItem = (AssetItem)item;
                return m_project.ToType(assetItem) == typeof(Scene);
            }
            return false;
        }

        private void OnItemDataBinding(object sender, VirtualizingTreeViewItemDataBindingArgs e)
        {
            ProjectItem item = e.Item as ProjectItem;
            if (item != null)
            {
                Text text = e.ItemPresenter.GetComponentInChildren<Text>(true);
                text.text = item.Name;

                Image image = e.ItemPresenter.GetComponentInChildren<Image>(true);
                if (IsScene(item))
                {
                    image.sprite = SceneIcon;
                }
                else
                {
                    image.sprite = FolderIcon;
                }
                image.gameObject.SetActive(true);
                e.HasChildren = item.Children != null && item.Children.Count(projectItem => projectItem.IsFolder || IsScene(projectItem)) > 0;
            }
        }

        private void OnItemExpanding(object sender, VirtualizingItemExpandingArgs e)
        {
            ProjectItem item = e.Item as ProjectItem;
            if (item != null)
            {
                e.Children = item.Children.Where(projectItem => projectItem.IsFolder).OrderBy(projectItem => projectItem.Name)
                    .Union(item.Children.Where(projectItem => IsScene(projectItem)).OrderBy(projectItem => projectItem.Name));
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedArgs e)
        {
            ProjectItem selectedItem = (ProjectItem)e.NewItem;
            if (selectedItem == null)
            {
                return;
            }
            if (IsScene(selectedItem))
            {
                Input.text = selectedItem.Name;
            }

            Input.ActivateInputField();
        }

        private void OnItemDoubleClick(object sender, ItemArgs e)
        {
            VirtualizingTreeViewItem treeViewItem = m_treeView.GetTreeViewItem(e.Items[0]);
            if (treeViewItem != null)
            {
                treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
            }

            Input.ActivateInputField();
        }


        private void SaveSceneToFolder(DialogCancelArgs args, ProjectItem folder)
        {
            if (folder.Children != null && folder.Children.Any(p => p.Name.ToLower() == Input.text.ToLower() && IsScene(p)))
            {
                Overwrite(folder.Children.Where(p => p.Name.ToLower() == Input.text.ToLower() && IsScene(p)).First());
                args.Cancel = true;
            }
            else
            {
                Editor.Undo.Purge();

                Editor.IsBusy = true;
                m_project.Create(m_project.Root, new byte[0], SceneManager.GetActiveScene(),  Input.text, (error, assetItem) =>
                {
                    Editor.IsBusy = false;
                    if (error.HasError)
                    {
                        m_windowManager.MessageBox("Unable to save scene", error.ErrorText);
                    }
//                    m_parentDialog.Close(null);
                });
            }
        }
    }
}

