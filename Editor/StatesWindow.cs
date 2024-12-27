using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Services;
using StateMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class StatesWindow : EditorWindow
{
    private bool _showTestStates;
    
    [MenuItem("JordanTama/States")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<StatesWindow>();
        wnd.titleContent = new GUIContent("States", EditorGUIUtility.IconContent("_Popup").image);
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    public void CreateGUI()
    {
        Draw();
    }

    private void Draw()
    {
        rootVisualElement.Clear();
        bool isPlaying = Locator.Get<Machine>(out var machine) && EditorApplication.isPlaying;
        
        // Runtime warning
        var warning = new HelpBox("Run the game to see the state machine.",
            HelpBoxMessageType.Warning)
        {
            style =
            {
                display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex
            }
        };
        rootVisualElement.Add(warning);

        // State names
        var stateNames = machine?.GetAllStates().ToList() ?? new List<string>();
        stateNames.Sort((a, b) =>
        {
            if (IsTest(a) && !IsTest(b))
                return 1;

            if (IsTest(b) && !IsTest(a))
                return -1;
            
            return string.Compare(a, b, StringComparison.Ordinal);
        });

        if (!_showTestStates)
        {
            for (int i = stateNames.Count - 1; i >= 0; i--)
            {
                if (IsTest(stateNames[i]))
                    stateNames.RemoveAt(i);
            }
        }
        
        // Left pane: ListView
        var listView = new ListView
        {
            makeItem = MakeItem,
            bindItem = BindItem,
            itemsSource = stateNames,
            name = "leftPane"
        };
        
        listView.selectionChanged += DrawRightPane;

        // Left pane: test toggle
        var testToggle = new Toggle("Show Test States")
        {
            value = _showTestStates,
            tooltip = "Show states starting with TEST STATE"
        };
        testToggle.RegisterValueChangedCallback(OnTestToggle);

        var leftPane = new VisualElement
        {
            style =
            {
                justifyContent = Justify.SpaceBetween
            }
        };
        leftPane.Add(listView);
        leftPane.Add(testToggle);
        
        // Right pane
        var rightPane = new VisualElement
        {
            name = "rightPane",
            style =
            {
                paddingLeft = 10
            }
        };
        
        // Split view
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal)
        {
            style =
            {
                display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None
            }
        };
        
        splitView.Add(leftPane);
        splitView.Add(rightPane);
        
        rootVisualElement.Add(splitView);
        return;

        VisualElement MakeItem()
        {
            var label = new Label
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft
                },
                name = "itemLabel"
            };

            var element = new VisualElement
            {
                style =
                {
                    justifyContent = Justify.SpaceBetween,
                    flexDirection = FlexDirection.Row
                }
            };
            
            element.Add(label);
            
            return element;
        }

        void BindItem(VisualElement item, int index)
        {
            // Label
            var label = item.Q<Label>("itemLabel");
            string state = stateNames[index];
            label.text = state;

            bool isCurrentState = machine.CurrentStateId == state;
            label.style.unityFontStyleAndWeight = isCurrentState ? FontStyle.Bold : FontStyle.Normal;

            // Dropdown
            var dropdown = (VisualElement) item.Q<DropdownField>();
            if (dropdown != null)
            {
                item.Remove(dropdown);
            }

            dropdown = CreateStateDropdown(state, false);
            
            item.Add(dropdown);
            
            // Test state styling
            if (!IsTest(state)) 
                return;

            label.style.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
            label.style.unityFontStyleAndWeight = isCurrentState ? FontStyle.BoldAndItalic : FontStyle.Italic;
        }

        void OnTestToggle(ChangeEvent<bool> evt)
        {
            if (_showTestStates == evt.newValue)
                return;
            
            _showTestStates = evt.newValue;
            Draw();
        }
        
        void DrawRightPane(IEnumerable<object> selections)
        {
            object selected = selections.FirstOrDefault();
            if (selected is not string state)
                return;

            var pane = rootVisualElement.Q("rightPane");
            if (pane == null)
                return;
        
            pane.Clear();

            var info = machine.GetStateInfo(state);

            // Parent
            var parentTitle = new Label("Parent")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            pane.Add(parentTitle);
            
            var parent = CreateStateLink(info.Parent);
            if (parent != null)
            {
                pane.Add(parent);
            }
            else
            {
                var noParent = new Label("No parent")
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleLeft,
                        paddingLeft = 10f,
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = new Color(0.5f, 0.5f, 0.5f, 1.0f)
                    }
                };
                pane.Add(noParent);
            }

            // Children
            var children = info.Children.Where(c => _showTestStates || !IsTest(c)).Select(CreateStateLink).ToArray();
        
            var childTitle = new Label("Children")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 10f
                }
            };

            pane.Add(childTitle);
            
            if (children.Length <= 0)
            {
                var noChildren = new Label("No children")
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleLeft,
                        paddingLeft = 10f,
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = new Color(0.5f, 0.5f, 0.5f, 1.0f)
                    }
                };
                pane.Add(noChildren);
                return;
            }
            
            foreach (var child in children)
                pane.Add(child);
        }
    }

    private VisualElement CreateStateLink(string state)
    {
        if (string.IsNullOrEmpty(state) || !Locator.Get<Machine>(out var machine))
            return default;

        bool isCurrentState = machine.CurrentStateId == state;
        
        var label = new Label(state)
        {
            style =
            {
                width = 200,
                unityTextAlign = TextAnchor.MiddleLeft,
                unityFontStyleAndWeight = isCurrentState
                    ? FontStyle.Bold
                    : FontStyle.Normal
            }
        };
        
        if (IsTest(state))
        {
            label.style.color = new Color(0.5f, 0.5f, 0.5f, 1.0f);
            label.style.unityFontStyleAndWeight = isCurrentState ? FontStyle.BoldAndItalic : FontStyle.Italic;
        }
        
        var stateDropdown = CreateStateDropdown(state);

        var element = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                paddingLeft = 10f
            }
        };
        element.Add(label);
        element.Add(stateDropdown);
        
        return element;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange newState)
    {
        Draw();
    }
    
    private static bool IsTest(string stateName) => stateName.StartsWith("TEST STATE");

    private DropdownField CreateStateDropdown(string state, bool showSelect = true)
    {
        var field = new DropdownField();
        field.choices.Clear();
        
        field.SetValueWithoutNotify("Options");
        
        if (showSelect)
            field.choices.Add("Select");

        if (Locator.Get<Machine>(out var machine) && state != machine.CurrentStateId)
        {
            field.choices.Add("Change State");
            field.choices.Add("Change State Async");
        }
        else
        {
            field.SetValueWithoutNotify("Active");
            field.SetEnabled(false);
        }
        
        field.RegisterValueChangedCallback(ValueChanged);
        return field;
        
        void ValueChanged(ChangeEvent<string> evt)
        {
            switch (evt.newValue)
            {
                case "Change State":
                    Locator.Get<Machine>().ChangeState(state);
                    break;
                
                case "Change State Async":
                    Locator.Get<Machine>().ChangeStateAsync(state).Forget();
                    break;
            }
            
            SelectState();
        }

        void SelectState()
        {
            field.SetValueWithoutNotify("Options");
            var leftPane = rootVisualElement.Q<ListView>("leftPane");
            leftPane.RefreshItems();
            leftPane.selectedIndex = leftPane.itemsSource.IndexOf(state);
        }
    }
}
