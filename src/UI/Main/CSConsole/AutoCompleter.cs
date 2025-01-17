﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityExplorer.Core.CSharp;
using UnityExplorer.Core.Input;
using UnityExplorer.Core.Runtime;
using UnityExplorer.Core.Unity;
using UnityExplorer.UI;
using UnityExplorer.UI.Main;

namespace UnityExplorer.UI.Main.CSConsole
{
    public class AutoCompleter
    {
        public static AutoCompleter Instance;

        public const int MAX_LABELS = 500;
        private const int UPDATES_PER_BATCH = 100;

        public static GameObject m_mainObj;

        private static readonly List<GameObject> m_suggestionButtons = new List<GameObject>();
        private static readonly List<Text> m_suggestionTexts = new List<Text>();
        private static readonly List<Text> m_hiddenSuggestionTexts = new List<Text>();

        private static bool m_suggestionsDirty;
        private static Suggestion[] m_suggestions = new Suggestion[0];
        private static int m_lastBatchIndex;

        private static string m_prevInput = "NULL";
        private static int m_lastCaretPos;

        public static void Init()
        {
            ConstructUI();

            m_mainObj.SetActive(false);
        }

        public static void Update()
        {
            if (!m_mainObj)
                return;

            if (!CSharpConsole.EnableAutocompletes)
            {
                if (m_mainObj.activeSelf)
                    m_mainObj.SetActive(false);

                return;
            }

            RefreshButtons();

            UpdatePosition();
        }

        public static void SetSuggestions(Suggestion[] suggestions)
        {
            m_suggestions = suggestions;

            m_suggestionsDirty = true;
            m_lastBatchIndex = 0;
        }

        private static void RefreshButtons()
        {
            if (!m_suggestionsDirty)
            {
                return;
            }

            if (m_suggestions.Length < 1)
            {
                if (m_mainObj.activeSelf)
                {
                    m_mainObj?.SetActive(false);
                }
                return;
            }

            if (!m_mainObj.activeSelf)
            {
                m_mainObj.SetActive(true);
            }

            if (m_suggestions.Length < 1 || m_lastBatchIndex >= MAX_LABELS)
            {
                m_suggestionsDirty = false;
                return;
            }

            int end = m_lastBatchIndex + UPDATES_PER_BATCH;
            for (int i = m_lastBatchIndex; i < end && i < MAX_LABELS; i++)
            {
                if (i >= m_suggestions.Length)
                {
                    if (m_suggestionButtons[i].activeSelf)
                    {
                        m_suggestionButtons[i].SetActive(false);
                    }
                }
                else
                {
                    if (!m_suggestionButtons[i].activeSelf)
                    {
                        m_suggestionButtons[i].SetActive(true);
                    }

                    var suggestion = m_suggestions[i];
                    var label = m_suggestionTexts[i];
                    var hiddenLabel = m_hiddenSuggestionTexts[i];

                    label.text = suggestion.Full;
                    hiddenLabel.text = suggestion.Addition;

                    label.color = suggestion.TextColor;
                }

                m_lastBatchIndex = i;
            }

            m_lastBatchIndex++;
        }

        private static void UpdatePosition()
        {
            try
            {
                var editor = CSharpConsole.Instance;

                if (!editor.InputField.isFocused)
                    return;

                var textGen = editor.InputText.cachedTextGenerator;
                int caretPos = editor.m_lastCaretPos;

                if (caretPos == m_lastCaretPos)
                    return;

                m_lastCaretPos = caretPos;

                if (caretPos >= 1)
                    caretPos--;

                var pos = textGen.characters[caretPos].cursorPos;

                pos = editor.InputField.transform.TransformPoint(pos);

                m_mainObj.transform.position = new Vector3(pos.x + 10, pos.y - 20, 0);
            }
            catch //(Exception e)
            {
                //ExplorerCore.Log(e.ToString());
            }
        }

        private static readonly char[] splitChars = new[] { '{', '}', ',', ';', '<', '>', '(', ')', '[', ']', '=', '|', '&', '?' };

        public static void CheckAutocomplete()
        {
            var m_codeEditor = CSharpConsole.Instance;
            string input = m_codeEditor.InputField.text;
            int caretIndex = m_codeEditor.InputField.caretPosition;

            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    int start = caretIndex <= 0 ? 0 : input.LastIndexOfAny(splitChars, caretIndex - 1) + 1;
                    input = input.Substring(start, caretIndex - start).Trim();
                }
                catch (ArgumentException) { }
            }

            if (!string.IsNullOrEmpty(input) && input != m_prevInput)
            {
                GetAutocompletes(input);
            }
            else
            {
                ClearAutocompletes();
            }

            m_prevInput = input;
        }

        public static void ClearAutocompletes()
        {
            if (CSharpConsole.AutoCompletes.Any())
            {
                CSharpConsole.AutoCompletes.Clear();
            }
        }

        public static void GetAutocompletes(string input)
        {
            try
            {
                // Credit ManylMarco
                CSharpConsole.AutoCompletes.Clear();
                string[] completions = CSharpConsole.Instance.Evaluator.GetCompletions(input, out string prefix);
                if (completions != null)
                {
                    if (prefix == null)
                    {
                        prefix = input;
                    }

                    CSharpConsole.AutoCompletes.AddRange(completions
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Select(x => new Suggestion(x, prefix, Suggestion.Contexts.Other))
                        );
                }

                string trimmed = input.Trim();
                if (trimmed.StartsWith("using"))
                {
                    trimmed = trimmed.Remove(0, 5).Trim();
                }

                IEnumerable<Suggestion> namespaces = Suggestion.Namespaces
                    .Where(x => x.StartsWith(trimmed) && x.Length > trimmed.Length)
                    .Select(x => new Suggestion(
                        x.Substring(trimmed.Length),
                        x.Substring(0, trimmed.Length),
                        Suggestion.Contexts.Namespace));

                CSharpConsole.AutoCompletes.AddRange(namespaces);

                IEnumerable<Suggestion> keywords = Suggestion.Keywords
                    .Where(x => x.StartsWith(trimmed) && x.Length > trimmed.Length)
                    .Select(x => new Suggestion(
                        x.Substring(trimmed.Length),
                        x.Substring(0, trimmed.Length),
                        Suggestion.Contexts.Keyword));

                CSharpConsole.AutoCompletes.AddRange(keywords);
            }
            catch (Exception ex)
            {
                ExplorerCore.Log("Autocomplete error:\r\n" + ex.ToString());
                ClearAutocompletes();
            }
        }

        #region UI Construction

        private static void ConstructUI()
        {
            var parent = UIManager.CanvasRoot;

            var obj = UIFactory.CreateScrollView(parent, "AutoCompleterScrollView", out GameObject content, out _, new Color(0.1f, 0.1f, 0.1f, 0.95f));

            m_mainObj = obj;

            var mainRect = obj.GetComponent<RectTransform>();
            //m_thisRect = mainRect;
            mainRect.pivot = new Vector2(0f, 1f);
            mainRect.anchorMin = new Vector2(0.45f, 0.45f);
            mainRect.anchorMax = new Vector2(0.65f, 0.6f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            var mainGroup = content.GetComponent<VerticalLayoutGroup>();
            mainGroup.SetChildControlHeight(false);
            mainGroup.SetChildControlWidth(true);
            mainGroup.childForceExpandHeight = false;
            mainGroup.childForceExpandWidth = true;

            for (int i = 0; i < MAX_LABELS; i++)
            {
                var btn = UIFactory.CreateButton(content, "AutoCompleteButton", "", null);
                RuntimeProvider.Instance.SetColorBlock(btn, new Color(0, 0, 0, 0), highlighted: new Color(0.2f, 0.2f, 0.2f, 1.0f));

                var nav = btn.navigation;
                nav.mode = Navigation.Mode.Vertical;
                btn.navigation = nav;

                UIFactory.SetLayoutElement(btn.gameObject, minHeight: 20);

                var text = btn.GetComponentInChildren<Text>();
                text.alignment = TextAnchor.MiddleLeft;
                text.color = Color.white;

                var hiddenChild = UIFactory.CreateUIObject("HiddenText", btn.gameObject);
                hiddenChild.SetActive(false);
                var hiddenText = hiddenChild.AddComponent<Text>();
                m_hiddenSuggestionTexts.Add(hiddenText);
                btn.onClick.AddListener(() => { CSharpConsole.Instance.UseAutocomplete(hiddenText.text); });

                m_suggestionButtons.Add(btn.gameObject);
                m_suggestionTexts.Add(text);
            }
        }

        #endregion
    }
}
