using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Services;
using StateMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ViewTreeWindow : EditorWindow
{
    private ListView _leftPane;
    private VisualElement _rightPane;
    
    private readonly List<string> _stateNames = new();
    private readonly Dictionary<string, StateInfo> _map = new();
    
    [MenuItem("Window/UI Toolkit/View Active Machine")]
    public static void ShowExample()
    {
        var wnd = GetWindow<ViewTreeWindow>();
        wnd.titleContent = new GUIContent("Active State Machine");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        var root = rootVisualElement;
        
        if (!Locator.Get<Machine>(out var machine))
        {
            var warning = new Label("Ensure the game is running to view the active state machine.");
            root.Add(warning);
            return;
        }

        // Setup state info
        _stateNames.Clear();
        _map.Clear();
        foreach (string id in machine.GetAllStates())
        {
            _stateNames.Add(id);
            
            var info = machine.GetStateInfo(id);
            _map[id] = info;
        }

        // Right pane
        _rightPane = new VisualElement();
        
        // Left pane
        _leftPane = new ListView
        {
            makeItem = () => new Label(),
            bindItem = (item, index) =>
            {
                var label = (Label)item;
                label.text = _stateNames[index];
            },
            itemsSource = _stateNames
        };
        
        _leftPane.selectionChanged += DrawRightPane;
        
        var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        splitView.Add(_leftPane);
        splitView.Add(_rightPane);
        
        root.Add(splitView);
    }

    private void SelectState(string state)
    {
        _leftPane.selectedIndex = _stateNames.IndexOf(state);
    }

    private void DrawRightPane(IEnumerable<object> selections)
    {
        object selected = selections.FirstOrDefault();
        if (selected is not string state)
            return;
            
        if (!_map.TryGetValue(state, out var info))
            return;

        var parent = CreateStateLink(info.Parent);
        var children = info.Children.Select(CreateStateLink).ToArray();

        _rightPane.Clear();
        
        _rightPane.Add(new Label(info.Name));
        _rightPane.Add(CreateChangeStateElement(state));

        if (parent != null)
        {
            _rightPane.Add(new Label("Parent"));
            _rightPane.Add(parent);
        }

        if (children.Length > 0)
        {
            _rightPane.Add(new Label("Children"));
            foreach (var link in children)
                _rightPane.Add(link);
        }
    }

    private VisualElement CreateStateLink(string state)
    {
        if (string.IsNullOrEmpty(state))
            return default;
        
        var label = new Label(state);
        var selectButton = new Button(() => SelectState(state)) { text = "Select" };

        var element = new VisualElement();
        element.Add(label);
        element.Add(selectButton);
        
        return element;
    }

    private VisualElement CreateChangeStateElement(string state)
    {
        var changeButton = new Button(() => Locator.Get<Machine>().ChangeState(state)) { text = "Change State" };
        var changeAsyncButton = new Button(() => Locator.Get<Machine>().ChangeStateAsync(state).Forget())
            { text = "Change State Async" };
        
        var element = new VisualElement();
        element.Add(changeButton);
        element.Add(changeAsyncButton);
        return element;
    }
}
