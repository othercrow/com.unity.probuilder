﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine.Assertions;
using UnityEngine.ProBuilder;
using UObject = UnityEngine.Object;
using UnityEngine.ProBuilder.AssetIdRemapUtility;
using UnityEngine.ProBuilder.MeshOperations;

class TempMenuItems : EditorWindow
{
	Vector2 m_Scroll;
	IEnumerable<IPref> m_Settings;


	[MenuItem("Tools/Temp Menu Item &d", false, 1000)]
	static void MenuInit()
	{
		GetWindow<TempMenuItems>();
	}

	void OnEnable()
	{
		m_Settings = UserSettings.FindUserSettings();
	}


	void OnGUI()
	{
		GUILayout.Label("count: " + m_Settings.Count());

		m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
		foreach (var item in m_Settings)
		{
			var val = item.GetValue();
			GUILayout.Label(item.key + "  " + (val != null ? val.ToString() : "null") + "   " + item.scope);
		}
		EditorGUILayout.EndScrollView();
	}

	public static void SaveMeshTemplate(Mesh mesh)
	{
//		StackTrace trace = new StackTrace(1, true);
//		for (int i = 0; i < trace.FrameCount; i++)
//		{
//			StackFrame first = trace.GetFrame(i);
//			UnityEngine.Debug.Log(first.GetFileName() + ": " + first.GetMethod());
//		}
	}
}
