﻿using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityObject = UnityEngine.Object;
using System.IO;

namespace Battlehub.RTSaveLoad2
{

    public class PersistentClassMapperGUI
    {
        private Type m_baseType;
        private Func<Type, string, bool> m_groupFilter;
        private string[] m_groupNames;
        private string m_groupLabel;
        private int m_selectedGroupIndex;
        private string m_filterText = string.Empty;
        private Vector2 m_scrollViewPosition;
        private Type[] m_types;

        private Dictionary<Type, int> m_typeToIndex;

        private bool IsAllSelected
        {
            get { return m_selectedCount == m_mappings.Length; }
        }

        private bool IsNoneSelected
        {
            get { return m_selectedCount == 0; }
        }

        private int m_selectedCount;
        private int[] m_filteredTypeIndices;
        private class ClassMappingInfo
        {
            public int PersistentPropertyTag;
            public int PersistentSubclassTag;
            public bool IsEnabled;
            public bool IsExpanded;
            public bool[] IsParentExpanded;
            public int ExpandedCounter;
            public PersistentPropertyMapping[] PropertyMappings;
            public PersistentSubclass[] Subclasses;
            public bool[] IsPropertyMappingEnabled;
            public string[][] PropertyMappingNames; //per property
            public string[][] PropertyMappingTypeNames; //per property
            public string[][] PropertyMappingNamespaces;
            public string[][] PropertyMappingAssemblyNames;
            public int[] PropertyMappingSelection;

            public bool IsSupportedPlaftormsSectionExpanded;
            public HashSet<RuntimePlatform> UnsupportedPlatforms;
        }

        private ClassMappingInfo[] m_mappings;
        private string m_mappingStoragePath;
        private string m_mappingTemplateStoragePath;
        private CodeGen m_codeGen;

        public PersistentClassMapperGUI(CodeGen codeGen, string mappingStorage, string mappingTemplateStorage, Type baseType, Type[] types, string[] groupNames, string groupLabel, Func<Type, string, bool> groupFilter)
        {
            m_mappingStoragePath = mappingStorage;
            m_mappingTemplateStoragePath = mappingTemplateStorage;
            m_codeGen = codeGen;
            m_baseType = baseType;
            m_types = types;
            m_groupNames = groupNames;
            m_groupLabel = groupLabel;
            m_groupFilter = groupFilter;
        }

        public void OnGUI()
        {
            if (m_mappings == null)
            {
                Initialize();
                LoadMappings();
            }

            EditorGUILayout.Separator();
            EditorGUI.BeginChangeCheck();

            m_selectedGroupIndex = EditorGUILayout.Popup(m_groupLabel, m_selectedGroupIndex, m_groupNames);
            m_filterText = EditorGUILayout.TextField("Type Filter:", m_filterText);

            if (EditorGUI.EndChangeCheck())
            {
                List<int> filteredTypeIndices = new List<int>();
                for (int i = 0; i < m_types.Length; ++i)
                {
                    Type type = m_types[i];
                    if (m_codeGen.TypeName(type).ToLower().Contains(m_filterText.ToLower()) && (m_selectedGroupIndex == 0 || m_groupFilter(type, m_groupNames[m_selectedGroupIndex])))
                    {
                        filteredTypeIndices.Add(i);
                    }
                }
                m_filteredTypeIndices = filteredTypeIndices.ToArray();

            }

            EditorGUI.BeginChangeCheck();

            if (IsAllSelected)
            {
                GUILayout.Toggle(true, "Select All");
            }
            else if (IsNoneSelected)
            {
                GUILayout.Toggle(false, "Select All");
            }
            else
            {
                GUILayout.Toggle(false, "Select All", "ToggleMixed");
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (IsAllSelected)
                {
                    UnselectAll();
                }
                else
                {
                    SelectAll();
                }
            }

            EditorGUILayout.Separator();

            EditorGUI.BeginChangeCheck();
            GUILayout.Button("Reset");
            if (EditorGUI.EndChangeCheck())
            {
                UnselectAll();
                LoadMappings();
            }

            EditorGUILayout.Separator();
            m_scrollViewPosition = EditorGUILayout.BeginScrollView(m_scrollViewPosition);

            EditorGUILayout.BeginVertical();
            {
                for (int i = 0; i < m_filteredTypeIndices.Length; ++i)
                {
                    int typeIndex = m_filteredTypeIndices[i];
                    DrawTypeEditor(typeIndex, typeIndex);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();  
        }

        private void Initialize()
        {
            m_mappings = new ClassMappingInfo[m_types.Length];
            for (int i = 0; i < m_types.Length; ++i)
            {
                m_mappings[i] = new ClassMappingInfo();
            }

            m_typeToIndex = new Dictionary<Type, int>();
            m_filteredTypeIndices = new int[m_types.Length];
            for (int i = 0; i < m_filteredTypeIndices.Length; ++i)
            {
                m_filteredTypeIndices[i] = i;
                m_typeToIndex.Add(m_types[i], i);
                m_mappings[i].IsParentExpanded = new bool[GetAncestorsCount(m_types[i])];
            }
        }

        private void SelectAll()
        {
            for (int i = 0; i < m_mappings.Length; ++i)
            {
                m_mappings[i].IsEnabled = true;
                TryExpandType(i);
                for (int j = 0; j < m_mappings[i].IsPropertyMappingEnabled.Length; ++j)
                {
                    m_mappings[i].IsPropertyMappingEnabled[j] = true;
                }
            }
            m_selectedCount = m_mappings.Length;
        }

        private void UnselectAll()
        {
            for (int i = 0; i < m_mappings.Length; ++i)
            {
                m_mappings[i].IsEnabled = false;
                TryExpandType(i);

                if (m_mappings[i].IsPropertyMappingEnabled != null)
                {
                    for (int j = 0; j < m_mappings[i].IsPropertyMappingEnabled.Length; ++j)
                    {
                        m_mappings[i].IsPropertyMappingEnabled[j] = false;
                    }
                }
            }
            m_selectedCount = 0;
        }

        private void LoadMappings()
        {
            GameObject storageGO = (GameObject)AssetDatabase.LoadAssetAtPath(m_mappingStoragePath, typeof(GameObject));
            if (storageGO == null)
            {
                storageGO = (GameObject)AssetDatabase.LoadAssetAtPath(m_mappingTemplateStoragePath, typeof(GameObject));
            }

            if (storageGO != null)
            {
                PersistentClassMapping[] mappings = storageGO.GetComponentsInChildren<PersistentClassMapping>(true);
                for (int i = 0; i < mappings.Length; ++i)
                {
                    PersistentClassMapping classMapping = mappings[i];
                    Type type = Type.GetType(classMapping.MappedAssemblyQualifiedName);
                    int typeIndex;
                    if (type != null && m_typeToIndex.TryGetValue(type, out typeIndex))
                    {
                        PersistentPropertyMapping[] pMappings = classMapping.PropertyMappings;
                        PersistentSubclass[] subclasses = classMapping.Subclasses;
                        m_mappings[typeIndex].PropertyMappings = pMappings;
                        m_mappings[typeIndex].Subclasses = subclasses;
                        m_mappings[typeIndex].IsEnabled = classMapping.IsEnabled;
                        m_mappings[typeIndex].PersistentPropertyTag = classMapping.PersistentPropertyTag;
                        m_mappings[typeIndex].PersistentSubclassTag = classMapping.PersistentSubclassTag;
                        m_selectedCount++;
                        ExpandType(typeIndex);
                    }
                }
            }
            ExpandType(0);
            m_mappings[0].IsEnabled = true;
        }

        public PersistentClassMapping[] SaveMappings()
        {
            GameObject storageGO = (GameObject)AssetDatabase.LoadAssetAtPath(m_mappingStoragePath, typeof(GameObject));
            if (storageGO == null)
            {
                storageGO = (GameObject)AssetDatabase.LoadAssetAtPath(m_mappingTemplateStoragePath, typeof(GameObject));
            }

            Dictionary<string, PersistentClassMapping> existingMappings;
            if (storageGO != null)
            {
                storageGO = UnityObject.Instantiate(storageGO);
                existingMappings = storageGO.GetComponentsInChildren<PersistentClassMapping>(true).ToDictionary(m => m.name);
            }
            else
            {
                storageGO = new GameObject();
                existingMappings = new Dictionary<string, PersistentClassMapping>();
            }

            Dictionary<int, Dictionary<string, PersistentSubclass>> typeIndexToSubclasses = new Dictionary<int, Dictionary<string, PersistentSubclass>>();
            for (int typeIndex = 0; typeIndex < m_mappings.Length; ++typeIndex)
            {
                ClassMappingInfo mapping = m_mappings[typeIndex];
                if (!mapping.IsEnabled)
                {
                    continue;
                }
                Dictionary<string, PersistentSubclass> subclassDictionary;
                if (mapping.Subclasses == null)
                {
                    subclassDictionary = new Dictionary<string, PersistentSubclass>();
                }
                else
                {
                    for (int i = 0; i < mapping.Subclasses.Length; ++i)
                    {
                        PersistentSubclass subclass = mapping.Subclasses[i];
                        subclass.IsEnabled = false;
                    }

                    subclassDictionary = mapping.Subclasses.ToDictionary(s => s.FullTypeName);
                }

                typeIndexToSubclasses.Add(typeIndex, subclassDictionary);
            }

            for (int typeIndex = 0; typeIndex < m_mappings.Length; ++typeIndex)
            {
                ClassMappingInfo mapping = m_mappings[typeIndex];
                if (!mapping.IsEnabled)
                {
                    continue;
                }


                Type type = m_types[typeIndex];
                Type baseType = GetEnabledBaseType(typeIndex);
                if (baseType == null)
                {
                    continue;
                }

                int baseTypeIndex;
                if (m_typeToIndex.TryGetValue(baseType, out baseTypeIndex))
                {
                    ClassMappingInfo baseClassMapping = m_mappings[baseTypeIndex];
                    string ns = PersistentClassMapping.ToPersistentNamespace(m_types[typeIndex].Namespace);
                    string typeName = PersistentClassMapping.ToPersistentName(m_types[typeIndex].Name);
                    string fullTypeName = string.Format("{0}.{1}", ns, typeName);

                    Dictionary<string, PersistentSubclass> subclassDictionary = typeIndexToSubclasses[baseTypeIndex];
                    if (!subclassDictionary.ContainsKey(fullTypeName))
                    {
                        PersistentSubclass subclass = new PersistentSubclass();
                        subclass.IsEnabled = true;
                        subclass.Namespace = PersistentClassMapping.ToPersistentNamespace(type.Namespace);
                        subclass.TypeName = PersistentClassMapping.ToPersistentName(m_codeGen.TypeName(type));
                        baseClassMapping.PersistentSubclassTag++;
                        subclass.PersistentTag = baseClassMapping.PersistentSubclassTag;

                        subclassDictionary.Add(fullTypeName, subclass);
                    }
                }
            }

            PersistentClassMapping[] savedMappings = new PersistentClassMapping[m_mappings.Length];
            for (int typeIndex = 0; typeIndex < m_mappings.Length; ++typeIndex)
            {
                if (m_mappings[typeIndex].PropertyMappings == null)
                {
                    continue;
                }

                PersistentClassMapping classMapping;
                if (!existingMappings.TryGetValue(m_types[typeIndex].FullName, out classMapping))
                {
                    GameObject typeStorageGO = new GameObject();
                    typeStorageGO.transform.SetParent(storageGO.transform, false);
                    typeStorageGO.name = m_types[typeIndex].FullName;
                    classMapping = typeStorageGO.AddComponent<PersistentClassMapping>();
                }

                savedMappings[typeIndex] = classMapping;

                PersistentPropertyMapping[] propertyMappings = m_mappings[typeIndex].PropertyMappings;
                int[] propertyMappingsSelection = m_mappings[typeIndex].PropertyMappingSelection;
                List<PersistentPropertyMapping> selectedPropertyMappings = new List<PersistentPropertyMapping>();
                for (int propIndex = 0; propIndex < propertyMappings.Length; ++propIndex)
                {
                    PersistentPropertyMapping propertyMapping = propertyMappings[propIndex];
                    propertyMapping.IsEnabled = m_mappings[typeIndex].IsPropertyMappingEnabled[propIndex];

                    if (propertyMappingsSelection[propIndex] >= 0)
                    {
                        propertyMapping.MappedName = m_mappings[typeIndex].PropertyMappingNames[propIndex][propertyMappingsSelection[propIndex]];
                        propertyMapping.MappedTypeName = m_mappings[typeIndex].PropertyMappingTypeNames[propIndex][propertyMappingsSelection[propIndex]];
                        propertyMapping.MappedNamespace = m_mappings[typeIndex].PropertyMappingNamespaces[propIndex][propertyMappingsSelection[propIndex]];
                        propertyMapping.MappedAssemblyName = m_mappings[typeIndex].PropertyMappingAssemblyNames[propIndex][propertyMappingsSelection[propIndex]];
                        if (propertyMapping.PersistentTag == 0)
                        {
                            m_mappings[typeIndex].PersistentPropertyTag++;
                            propertyMapping.PersistentTag = m_mappings[typeIndex].PersistentPropertyTag;
                        }

                        selectedPropertyMappings.Add(propertyMapping);
                    }
                }

                m_mappings[typeIndex].PropertyMappings = selectedPropertyMappings.ToArray();
                ExpandType(typeIndex);

                classMapping.IsEnabled = m_mappings[typeIndex].IsEnabled;
                classMapping.PersistentPropertyTag = m_mappings[typeIndex].PersistentPropertyTag;
                classMapping.PersistentSubclassTag = m_mappings[typeIndex].PersistentSubclassTag;
                classMapping.PropertyMappings = selectedPropertyMappings.ToArray();
                if (typeIndexToSubclasses.ContainsKey(typeIndex))
                {
                    classMapping.Subclasses = typeIndexToSubclasses[typeIndex].Values.ToArray();
                }
                classMapping.MappedAssemblyName = m_types[typeIndex].Assembly.FullName.Split(',')[0];
                classMapping.MappedNamespace = m_types[typeIndex].Namespace;
                classMapping.MappedTypeName = m_types[typeIndex].Name;
                classMapping.PersistentNamespace = PersistentClassMapping.ToPersistentNamespace(classMapping.MappedNamespace);
                classMapping.PersistentTypeName = PersistentClassMapping.ToPersistentName(m_types[typeIndex].Name);

                Type baseType = GetEnabledBaseType(typeIndex);
                if (baseType == null)
                {
                    classMapping.PersistentBaseNamespace = typeof(PersistentSurrogate).Namespace;
                    classMapping.PersistentBaseTypeName = typeof(PersistentSurrogate).Name;
                }
                else
                {
                    classMapping.PersistentBaseNamespace = PersistentClassMapping.ToPersistentNamespace(baseType.Namespace);
                    classMapping.PersistentBaseTypeName = PersistentClassMapping.ToPersistentName(m_codeGen.TypeName(baseType));
                }

            }

            PrefabUtility.CreatePrefab(m_mappingStoragePath, storageGO);
            UnityObject.DestroyImmediate(storageGO);

            return savedMappings;
        }

        private Type GetEnabledBaseType(int typeIndex)
        {
            Type baseType = null;
            Type type = m_types[typeIndex];
            while (true)
            {
                type = type.BaseType;
                if (type == m_baseType)
                {
                    baseType = type;
                    break;
                }

                if (type == null)
                {
                    break;
                }

                int baseIndex;
                if (m_typeToIndex.TryGetValue(type, out baseIndex))
                {
                    if (m_mappings[baseIndex].IsEnabled)
                    {
                        baseType = type;
                        break;
                    }
                }
            }

            return baseType;
        }

        private int GetAncestorsCount(Type type)
        {
            int count = 0;
            while (type != null && type.BaseType != m_baseType)
            {
                count++;
                type = type.BaseType;
            }
            return count;
        }

        private GUIContent m_guiContent = new GUIContent();
        private void DrawTypeEditor(int rootTypeIndex, int typeIndex, int indent = 1)
        {
            Type type = m_types[typeIndex];
            if (type == m_baseType)
            {
                return;
            }

            string label = m_codeGen.TypeName(type);
            bool isExpandedChanged;
            bool isExpanded;
            bool isSelectionChanged;

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            {
                GUILayout.Space(5 + 18 * (indent - 1));
                EditorGUI.BeginChangeCheck();

                m_mappings[typeIndex].IsEnabled = EditorGUILayout.Toggle(m_mappings[typeIndex].IsEnabled, GUILayout.MaxWidth(15));

                isSelectionChanged = EditorGUI.EndChangeCheck();

                EditorGUI.BeginChangeCheck();
                if (indent == 1)
                {
                    m_mappings[typeIndex].IsExpanded = EditorGUILayout.Foldout(m_mappings[typeIndex].IsExpanded, label, true);
                    isExpanded = m_mappings[typeIndex].IsExpanded;
                }
                else
                {
                    m_mappings[rootTypeIndex].IsParentExpanded[indent - 2] = EditorGUILayout.Foldout(m_mappings[rootTypeIndex].IsParentExpanded[indent - 2], label, true);
                    isExpanded = m_mappings[rootTypeIndex].IsParentExpanded[indent - 2];
                }
                isExpandedChanged = EditorGUI.EndChangeCheck();
            }
            EditorGUILayout.EndHorizontal();

            if (isExpandedChanged || isSelectionChanged)
            {
                if (isExpandedChanged)
                {
                    m_mappings[typeIndex].ExpandedCounter = isExpanded ?
                        m_mappings[typeIndex].ExpandedCounter + 1 :
                        m_mappings[typeIndex].ExpandedCounter - 1;
                }

                if (isSelectionChanged)
                {
                    if (m_mappings[typeIndex].IsEnabled)
                    {
                        m_selectedCount++;
                    }
                    else
                    {
                        m_selectedCount--;
                    }
                }

                TryExpandType(typeIndex);
            }

            if (isExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Space(5 + 18 * indent);
                    EditorGUILayout.BeginVertical();
                    m_mappings[typeIndex].IsSupportedPlaftormsSectionExpanded = EditorGUILayout.Foldout(m_mappings[typeIndex].IsSupportedPlaftormsSectionExpanded, "Supported Platforms");
                    if (m_mappings[typeIndex].IsSupportedPlaftormsSectionExpanded)
                    {

                        string[] platformNames = Enum.GetNames(typeof(RuntimePlatform));
                        RuntimePlatform[] platforms = (RuntimePlatform[])Enum.GetValues(typeof(RuntimePlatform));

                        for (int i = 0; i < platformNames.Length; ++i)
                        {

                            EditorGUI.BeginChangeCheck();
                            bool platformChecked = EditorGUILayout.Toggle(platformNames[i], m_mappings[typeIndex].UnsupportedPlatforms == null || !m_mappings[typeIndex].UnsupportedPlatforms.Contains(platforms[i]));
                            if (EditorGUI.EndChangeCheck())
                            {
                                if (m_mappings[typeIndex].UnsupportedPlatforms == null)
                                {
                                    m_mappings[typeIndex].UnsupportedPlatforms = new HashSet<RuntimePlatform>();
                                }
                                if (platformChecked)
                                {
                                    m_mappings[typeIndex].UnsupportedPlatforms.Remove(platforms[i]);
                                }
                                else
                                {
                                    m_mappings[typeIndex].UnsupportedPlatforms.Add(platforms[i]);
                                }
                            }

                        }
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginVertical();
                {
                    for (int propIndex = 0; propIndex < m_mappings[typeIndex].PropertyMappings.Length; ++propIndex)
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(5 + 18 * indent);

                            PersistentPropertyMapping pMapping = m_mappings[typeIndex].PropertyMappings[propIndex];

                            m_mappings[typeIndex].IsPropertyMappingEnabled[propIndex] = EditorGUILayout.Toggle(m_mappings[typeIndex].IsPropertyMappingEnabled[propIndex], GUILayout.MaxWidth(20));

                            m_guiContent.text = pMapping.PersistentName;
                            m_guiContent.tooltip = pMapping.PersistentTypeName;
                            EditorGUILayout.LabelField(m_guiContent, GUILayout.MaxWidth(230));

                            int newPropertyIndex = EditorGUILayout.Popup(m_mappings[typeIndex].PropertyMappingSelection[propIndex], m_mappings[typeIndex].PropertyMappingNames[propIndex]);
                            m_mappings[typeIndex].PropertyMappingSelection[propIndex] = newPropertyIndex;

                            EditorGUI.BeginChangeCheck();
                            GUILayout.Button("X", GUILayout.Width(20));
                            if (EditorGUI.EndChangeCheck())
                            {
                                m_mappings[typeIndex].PropertyMappingSelection[propIndex] = -1;
                            }

                            EditorGUILayout.LabelField("Slot: " + pMapping.PersistentTag, GUILayout.Width(60));
                        }
                        EditorGUILayout.EndHorizontal();
                    }




                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Space(5 + 18 * indent);
                        GUILayout.Button("Edit", GUILayout.Width(100));

                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(5 + 18 * indent);
                    EditorGUILayout.HelpBox("Template for this script compiled with errors. Please submit bug report to Battlehub@outlook.com" + Environment.NewLine + "Unity3d " + Application.unityVersion + "; RTSL Version " + RTSL2Version.Version, MessageType.Error);
                    EditorGUILayout.EndHorizontal();

                    if (type.BaseType != m_baseType)
                    {
                        int parentIndex;
                        if (m_typeToIndex.TryGetValue(type.BaseType, out parentIndex))
                        {
                            DrawTypeEditor(rootTypeIndex, parentIndex, indent + 1);
                        }
                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Separator();
            }
        }

        private void TryExpandType(int typeIndex)
        {
            if (m_mappings[typeIndex].PropertyMappings != null)
            {
                return;
            }
            if (m_mappings[typeIndex].ExpandedCounter > 0 || m_mappings[typeIndex].IsEnabled)
            {
                ExpandType(typeIndex);
            }
        }

        private void ExpandType(int typeIndex)
        {
            Type type = m_types[typeIndex];

            List<PersistentPropertyMapping> pMappings = new List<PersistentPropertyMapping>();
            List<bool> pMappingsEnabled = new List<bool>();

            PersistentPropertyMapping[] fieldMappings = m_mappings[typeIndex].PropertyMappings != null ?
                m_mappings[typeIndex].PropertyMappings.Where(p => !p.IsProperty).ToArray() :
                new PersistentPropertyMapping[0];

            HashSet<string> fieldMappingsHs = new HashSet<string>();
            IEnumerable<string> fmapKeys = fieldMappings.Select(fMap => fMap.PersistentFullTypeName + " " + fMap.PersistentName);
            foreach (string key in fmapKeys)
            {
                if (!fieldMappingsHs.Contains(key))
                {
                    fieldMappingsHs.Add(key);
                }
            }

            PersistentPropertyMapping[] propertyMappings = m_mappings[typeIndex].PropertyMappings != null ?
                m_mappings[typeIndex].PropertyMappings.Where(p => p.IsProperty).ToArray() :
                new PersistentPropertyMapping[0];

            HashSet<string> propertyMappingsHs = new HashSet<string>();
            IEnumerable<string> pmapKeys = propertyMappings.Select(pMap => pMap.PersistentFullTypeName + " " + pMap.PersistentName);
            foreach (string key in pmapKeys)
            {
                if (!propertyMappingsHs.Contains(key))
                {
                    propertyMappingsHs.Add(key);
                }
            }

            FieldInfo[] fields = m_codeGen.GetFields(type);
            HashSet<string> fieldHs = new HashSet<string>(fields.Select(fInfo => fInfo.FieldType.FullName + " " + fInfo.Name));

            PropertyInfo[] properties = m_codeGen.GetProperties(type);
            HashSet<string> propertyHs = new HashSet<string>(properties.Select(pInfo => pInfo.PropertyType.FullName + " " + pInfo.Name));

            for (int i = 0; i < fieldMappings.Length; ++i)
            {
                PersistentPropertyMapping mapping = fieldMappings[i];
                string key = mapping.MappedFullTypeName + " " + mapping.MappedName;
                if (!fieldHs.Contains(key))
                {
                    mapping.MappedName = null;
                    mapping.MappedTypeName = null;
                    mapping.MappedNamespace = null;
                    mapping.MappedAssemblyName = null;

                    pMappingsEnabled.Add(false);
                }
                else
                {
                    pMappingsEnabled.Add(mapping.IsEnabled);
                }

                pMappings.Add(mapping);
            }


            for (int f = 0; f < fields.Length; ++f)
            {
                FieldInfo fInfo = fields[f];

                string key = string.Format("{0}.{1}",
                    PersistentClassMapping.ToPersistentNamespace(fInfo.FieldType.Namespace),
                    m_codeGen.TypeName(fInfo.FieldType)) + " " + fInfo.Name;

                if (fieldMappingsHs.Contains(key))
                {
                    continue;
                }

                PersistentPropertyMapping pMapping = new PersistentPropertyMapping();
                pMapping.PersistentName = fInfo.Name;
                pMapping.PersistentTypeName = m_codeGen.TypeName(fInfo.FieldType);
                pMapping.PersistentNamespace = PersistentClassMapping.ToPersistentNamespace(fInfo.FieldType.Namespace);

                pMapping.MappedName = fInfo.Name;
                pMapping.MappedTypeName = m_codeGen.TypeName(fInfo.FieldType);
                pMapping.MappedNamespace = fInfo.FieldType.Namespace;
                pMapping.MappedAssemblyName = fInfo.FieldType.Assembly.FullName.Split(',')[0];
                pMapping.IsProperty = false;

                pMapping.UseSurrogate = m_codeGen.GetSurrogateType(fInfo.FieldType) != null;
                pMapping.HasDependenciesOrIsDependencyItself = m_codeGen.HasDependencies(fInfo.FieldType);

                pMappingsEnabled.Add(false);
                pMappings.Add(pMapping);
            }

            for (int i = 0; i < propertyMappings.Length; ++i)
            {
                PersistentPropertyMapping mapping = propertyMappings[i];
                string key = mapping.MappedFullTypeName + " " + mapping.MappedName;
                if (!propertyHs.Contains(key))
                {
                    mapping.MappedName = null;
                    mapping.MappedTypeName = null;
                    mapping.MappedNamespace = null;
                    mapping.MappedAssemblyName = null;

                    pMappingsEnabled.Add(false);
                }
                else
                {
                    pMappingsEnabled.Add(mapping.IsEnabled);
                }

                pMappings.Add(mapping);
            }


            for (int p = 0; p < properties.Length; ++p)
            {
                PropertyInfo pInfo = properties[p];

                string key = string.Format("{0}.{1}",
                    PersistentClassMapping.ToPersistentNamespace(pInfo.PropertyType.Namespace),
                    m_codeGen.TypeName(pInfo.PropertyType)) + " " + pInfo.Name;

                if (propertyMappingsHs.Contains(key))
                {
                    continue;
                }

                PersistentPropertyMapping pMapping = new PersistentPropertyMapping();

                pMapping.PersistentName = pInfo.Name;       //property name of mapping
                pMapping.PersistentTypeName = m_codeGen.TypeName(pInfo.PropertyType);
                pMapping.PersistentNamespace = PersistentClassMapping.ToPersistentNamespace(pInfo.PropertyType.Namespace);

                pMapping.MappedName = pInfo.Name;           //property name of unity type
                pMapping.MappedTypeName = m_codeGen.TypeName(pInfo.PropertyType);
                pMapping.MappedNamespace = pInfo.PropertyType.Namespace;
                pMapping.MappedAssemblyName = pInfo.PropertyType.Assembly.FullName.Split(',')[0];
                pMapping.IsProperty = true;

                pMapping.UseSurrogate = m_codeGen.GetSurrogateType(pInfo.PropertyType) != null;
                pMapping.HasDependenciesOrIsDependencyItself = m_codeGen.HasDependencies(pInfo.PropertyType);

                pMappingsEnabled.Add(false);
                pMappings.Add(pMapping);
            }

            m_mappings[typeIndex].PropertyMappings = pMappings.ToArray();
            m_mappings[typeIndex].IsPropertyMappingEnabled = pMappingsEnabled.ToArray();

            m_mappings[typeIndex].PropertyMappingNames = new string[pMappings.Count][];
            m_mappings[typeIndex].PropertyMappingTypeNames = new string[pMappings.Count][];
            m_mappings[typeIndex].PropertyMappingNamespaces = new string[pMappings.Count][];
            m_mappings[typeIndex].PropertyMappingAssemblyNames = new string[pMappings.Count][];
            m_mappings[typeIndex].PropertyMappingSelection = new int[pMappings.Count];

            string[][] mappedKeys = new string[pMappings.Count][];

            for (int propIndex = 0; propIndex < pMappings.Count; ++propIndex)
            {
                PersistentPropertyMapping pMapping = pMappings[propIndex];

                var propertyInfo = GetSuitableFields(fields, PersistentClassMapping.ToMappedNamespace(pMapping.PersistentNamespace) + "." + pMapping.PersistentTypeName)
                    .Select(f => new { Name = f.Name, Type = m_codeGen.TypeName(f.FieldType), Namespace = f.FieldType.Namespace, Assembly = f.FieldType.Assembly.FullName.Split(',')[0] })
                    .Union(GetSuitableProperties(properties, PersistentClassMapping.ToMappedNamespace(pMapping.PersistentNamespace) + "." + pMapping.PersistentTypeName)
                    .Select(p => new { Name = p.Name, Type = m_codeGen.TypeName(p.PropertyType), Namespace = p.PropertyType.Namespace, Assembly = p.PropertyType.Assembly.FullName.Split(',')[0] }))
                    .OrderBy(p => p.Name)
                    .ToArray();

                m_mappings[typeIndex].PropertyMappingNames[propIndex] = propertyInfo.Select(p => p.Name).ToArray();
                m_mappings[typeIndex].PropertyMappingTypeNames[propIndex] = propertyInfo.Select(p => p.Type).ToArray();
                m_mappings[typeIndex].PropertyMappingNamespaces[propIndex] = propertyInfo.Select(p => p.Namespace).ToArray();
                m_mappings[typeIndex].PropertyMappingAssemblyNames[propIndex] = propertyInfo.Select(p => p.Assembly).ToArray();
                mappedKeys[propIndex] = propertyInfo.Select(m => m.Namespace + "." + m.Type + " " + m.Name).ToArray();
            }

            for (int propIndex = 0; propIndex < m_mappings[typeIndex].PropertyMappingSelection.Length; ++propIndex)
            {
                PersistentPropertyMapping mapping = m_mappings[typeIndex].PropertyMappings[propIndex];

                m_mappings[typeIndex].PropertyMappingSelection[propIndex] = Array.IndexOf(mappedKeys[propIndex], mapping.MappedFullTypeName + " " + mapping.MappedName);
            }
        }

        private IEnumerable<PropertyInfo> GetSuitableProperties(PropertyInfo[] properties, string persistentType)
        {
            return properties.Where(pInfo => pInfo.PropertyType.FullName == persistentType);
        }

        private IEnumerable<FieldInfo> GetSuitableFields(FieldInfo[] fields, string persistentType)
        {
            return fields.Where(fInfo => fInfo.FieldType.FullName == persistentType);
        }
    }

    public class PersistentClassMapperWindow : EditorWindow
    {
        [MenuItem("Tools/Runtime SaveLoad2/Persistent Classes Mapper")]
        public static void ShowMenuItem()
        {
            ShowWindow();
        }

        public static void ShowWindow()
        {
            PersistentClassMapperWindow prevWindow = GetWindow<PersistentClassMapperWindow>();
            if (prevWindow != null)
            {
                prevWindow.Close();
            }

            PersistentClassMapperWindow window = CreateInstance<PersistentClassMapperWindow>();
            window.titleContent = new GUIContent("RTSL2 Config");
            window.Show();
            window.position = new Rect(20, 40, 1280, 768);
        }

        private Type[] m_mostImportantUOTypes =
        {
            typeof(UnityObject),
            typeof(GameObject),
            typeof(MeshRenderer),
            typeof(MeshFilter),
            typeof(SkinnedMeshRenderer),
            typeof(Mesh),
            typeof(Material),
            typeof(Rigidbody),
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(CapsuleCollider),
            typeof(MeshCollider),
            typeof(Camera),
            typeof(AudioClip),
            typeof(AudioSource),
            typeof(Light),
        };

        private Type[] m_mostImportantSurrogateTypes =
        {
            typeof(object),
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Vector2Int),
            typeof(Vector3Int),
            typeof(Color),
            typeof(Color32),
            typeof(Matrix4x4),
        };

        public const string SaveLoadRoot = @"/" + BHPath.Root + @"/RTSaveLoad2";
        public const string EditorPrefabsPath = SaveLoadRoot + "/Editor/Prefabs";

        public const string ClassMappingsStoragePath = "Assets" + EditorPrefabsPath + @"/ClassMappingsStorage.prefab";
        public const string ClassMappingsTemplatePath = "Assets" + EditorPrefabsPath + @"/ClassMappingsTemplate.prefab";
        public const string SurrogatesMappingsStoragePath = "Assets" + EditorPrefabsPath + @"/SurrogatesMappingsStorage.prefab";
        public const string SurrogatesMappingsTemplatePath = "Assets" + EditorPrefabsPath + @"/SurrogatesMappingsTemplate.prefab";

        public const string ScriptsAutoFolder = "Scripts_Auto";
        public const string PersistentClassesFolder = "PersistentClasses";


        private Type[] m_uoTypes;
        private PersistentClassMapperGUI m_uoMapperGUI;
        private PersistentClassMapperGUI m_surrogatesMapperGUI;
        private CodeGen m_codeGen = new CodeGen();

        private void GetUOAssembliesAndTypes(out Assembly[] assemblies, out Type[] types)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.Contains("UnityEngine")).OrderBy(a => a.FullName).ToArray();

            List<Type> allUOTypes = new List<Type>();
            List<Assembly> assembliesList = new List<Assembly>() { null };

            for (int i = 0; i < assemblies.Length; ++i)
            {
                Assembly assembly = assemblies[i];
                Type[] uoTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(UnityObject))).ToArray();
                if (uoTypes.Length > 0)
                {
                    assembliesList.Add(assembly);
                    allUOTypes.AddRange(uoTypes);
                }
            }

            for (int i = 0; i < m_mostImportantUOTypes.Length; ++i)
            {
                allUOTypes.Remove(m_mostImportantUOTypes[i]);
            }

            types = m_mostImportantUOTypes.Union(allUOTypes.OrderBy(t => m_codeGen.TypeName(t))).ToArray();
            assemblies = assembliesList.ToArray();
        }

        private void GetTypesRecursive(Type type, HashSet<Type> typesHS)
        {
            PropertyInfo[] properties = m_codeGen.GetAllProperties(type);
            FieldInfo[] fields = m_codeGen.GetFields(type);

            for (int p = 0; p < properties.Length; ++p)
            {
                PropertyInfo pInfo = properties[p];
                if (!typesHS.Contains(pInfo.PropertyType))
                {
                    Type surrogateType = m_codeGen.GetSurrogateType(pInfo.PropertyType);
                    if(surrogateType != null && !typesHS.Contains(surrogateType))
                    {
                        typesHS.Add(surrogateType);
                        GetTypesRecursive(surrogateType, typesHS);
                    }
                }
            }

            for (int f = 0; f < fields.Length; ++f)
            {
                FieldInfo fInfo = fields[f];
                if (!typesHS.Contains(fInfo.FieldType))
                {
                    Type surrogateType = m_codeGen.GetSurrogateType(fInfo.FieldType);
                    if (surrogateType != null && !typesHS.Contains(surrogateType))
                    {
                        typesHS.Add(surrogateType);
                        GetTypesRecursive(surrogateType, typesHS);
                    }
                }
            }
        }

        private void GetSurrogateAssembliesAndTypes(Type[] uoTypes, out Dictionary<string, HashSet<Type>> declaredIn, out Type[] types)
        {
            HashSet<Type> allTypesHS = new HashSet<Type>();
            declaredIn = new Dictionary<string, HashSet<Type>>();
            
            for(int typeIndex = 0; typeIndex < uoTypes.Length; ++typeIndex)
            {
                Type uoType = uoTypes[typeIndex];

                HashSet<Type> typesHs = new HashSet<Type>();
                GetTypesRecursive(uoType, typesHs);
                declaredIn.Add(uoType.Name, typesHs);

                foreach (Type type in typesHs)
                {
                    if(!allTypesHS.Contains(type))
                    {
                        allTypesHS.Add(type);
                    }
                }
            }

            for (int i = 0; i < m_mostImportantSurrogateTypes.Length; ++i)
            {
                allTypesHS.Remove(m_mostImportantSurrogateTypes[i]);
            }

            types = m_mostImportantSurrogateTypes.Union(allTypesHS.OrderBy(t => m_codeGen.TypeName(t))).ToArray();
        }

        private void OnGUI()
        {
            if(m_uoMapperGUI == null)
            {
                Assembly[] assemblies;
                GetUOAssembliesAndTypes(out assemblies, out m_uoTypes);
                m_uoMapperGUI = new PersistentClassMapperGUI(
                    m_codeGen, 
                    ClassMappingsStoragePath, 
                    ClassMappingsStoragePath, 
                    typeof(UnityObject), 
                    m_uoTypes, 
                    assemblies.Select(a => a == null ? "All" : a.GetName().Name).ToArray(),
                    "Assembly",
                    (type, groupName) => type.Assembly.GetName().Name == groupName);
            }

            if(m_surrogatesMapperGUI == null)
            {
                Type[] types;
                Dictionary<string, HashSet<Type>> declaredIn;
                GetSurrogateAssembliesAndTypes(m_uoTypes, out declaredIn, out types);
                m_surrogatesMapperGUI = new PersistentClassMapperGUI(
                    m_codeGen, 
                    SurrogatesMappingsStoragePath, 
                    SurrogatesMappingsTemplatePath, 
                    typeof(object),
                    types, 
                    new[] { "All" }.Union(declaredIn.Where(t => t.Value.Count > 0).Select(t => t.Key)).ToArray(), 
                    "Declaring Type",
                    (type, groupName) => declaredIn[groupName].Contains(type));
            }

            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(position.width / 2));
      
            m_uoMapperGUI.OnGUI();

            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(position.width / 2));
 
            m_surrogatesMapperGUI.OnGUI();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.HelpBox("Please note that most of the data are stored and restored using public properties which may cause undesired side effects. For example accessing renderer.material or meshfilter.mesh will instantiate new objects.", MessageType.Info);
            GUILayout.Button("Create Persistent Objects", GUILayout.Height(37));
            if (EditorGUI.EndChangeCheck())
            {
                PersistentClassMapping[] uoMappings = m_uoMapperGUI.SaveMappings();
                PersistentClassMapping[] surrogateMappings = m_surrogatesMapperGUI.SaveMappings();

                string scriptsAutoPath = Application.dataPath + SaveLoadRoot + "/" + ScriptsAutoFolder;
                Directory.Delete(scriptsAutoPath, true);
                Directory.CreateDirectory(scriptsAutoPath);
                string persistentClassesPath = scriptsAutoPath + "/" + PersistentClassesFolder;
                Directory.CreateDirectory(persistentClassesPath);
                
                CodeGen codeGen = new CodeGen();
                for(int i = 0; i < uoMappings.Length; ++i)
                {
                    PersistentClassMapping mapping = uoMappings[i];
                    string code = codeGen.CreatePersistentClass(mapping);
                    CreateCSFile(persistentClassesPath, mapping, code);
                }

                for (int i = 0; i < surrogateMappings.Length; ++i)
                {
                    PersistentClassMapping mapping = surrogateMappings[i];
                    string code = codeGen.CreatePersistentClass(mapping);
                    CreateCSFile(persistentClassesPath, mapping, code);
                }

                string typeModelCreatorCode = codeGen.CreateTypeModelCreator(uoMappings.Union(surrogateMappings).ToArray());
                File.WriteAllText(persistentClassesPath + "/TypeModelCreator.cs", typeModelCreatorCode);
            }
      
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();


        }

        private static void CreateCSFile(string persistentClassesPath, PersistentClassMapping mapping, string code)
        {
            File.WriteAllText(persistentClassesPath + "/" + mapping.PersistentFullTypeName.Replace(".", "_") + ".cs", code);
        }
    }
}
