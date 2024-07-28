using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Services;
using UnityEngine;
using Logger = Logging.Logger;

namespace StateMachine
{
    public class Machine : IServiceStandard
    {
        public event StateChange OnStateChangeComplete;
        
        private State _currentState;
        private readonly Dictionary<string, State> _states = new();

        public bool Initialized { get; private set; }
        public string CurrentStateId => _currentState?.Id ?? "";

        #region Internal Methods
        
        #endregion
        
        #region IServiceStandard Implementation

        public void OnRegistered()
        {
            // Create the initial state
            var baseStateConstructor = new StateConstructor(Constants.ROOT_STATE_NAME);

            // Get assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Get all types
            IEnumerable<Type> GetTypes(Assembly assembly) => assembly.GetTypes();
            var types = assemblies.SelectMany(GetTypes);

            // Get all methods with the correct binding flags
            const BindingFlags methodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = types.SelectMany(type => type.GetMethods(methodFlags));

            // Get the methods with the construction attribute
            bool HasAttribute(MemberInfo method) => method.GetCustomAttribute<ConstructStateMachineAttribute>() != null;
            var attributedMethods = methods.Where(HasAttribute).ToList();

            // Order the methods
            string GetDependency(MemberInfo method) =>
                method.GetCustomAttribute<ConstructStateMachineAttribute>().StateId;

            bool TryGetConstructor(string id, out StateConstructor constructor)
            {
                Queue<StateConstructor> queue = new();
                queue.Enqueue(baseStateConstructor);

                while (queue.Count > 0)
                {
                    var parent = queue.Dequeue();
                    foreach (var child in parent.Children)
                        queue.Enqueue(child);

                    if (parent.Id != id)
                        continue;
                    
                    constructor = parent;
                    return true;
                }

                constructor = null;
                return false;
            }

            // Construct the state machine
            while (attributedMethods.Count > 0)
            {
                // Get all the methods with their dependency ready, and remove from the attributedMethods
                var hasDependencies = new List<MethodInfo>();
                for (int i = attributedMethods.Count - 1; i >= 0; i--)
                {
                    var method = attributedMethods[i];
                    string dependencyId = GetDependency(method);
                    if (!TryGetConstructor(dependencyId, out _))
                        continue;
                    
                    hasDependencies.Add(method);
                    attributedMethods.RemoveAt(i);
                }

                // No methods have their dependency ready - break out of the loop
                if (hasDependencies.Count <= 0)
                    break;

                // Extract the priority from a method
                int GetPriority(MemberInfo method) =>
                    method.GetCustomAttribute<ConstructStateMachineAttribute>().Priority;
                
                // Order the methods by their priority
                var ordered = hasDependencies.OrderBy(GetPriority).ToArray();

                bool hasInvoked = false;
                foreach (var method in ordered)
                {
                    string dependencyName = GetDependency(method);
                    if (!TryGetConstructor(dependencyName, out var dependencyConstructor))
                        continue;

                    method.Invoke(null, new object[] {dependencyConstructor});
                    hasInvoked = true;
                }

                if (!hasInvoked)
                    break;
            }

            if (attributedMethods.Count > 0)
                Error($"Missing dependencies: {string.Join(", ", attributedMethods.Select(GetDependency))}");

            void RegisterConstructor(StateConstructor constructor)
            {
                foreach (var child in constructor.Children)
                    RegisterConstructor(child);
                
                string id = constructor.Id;
                if (_states.ContainsKey(id))
                {
                    Error($"Tried to register state with id '{id}', but it is already registered.");
                    return;
                }
        
                _states[id] = new State(constructor);
            }
            RegisterConstructor(baseStateConstructor);

            // Begin the state machine
            ChangeState(Constants.ROOT_STATE_NAME);
            Initialized = true;
        }

        public void OnUnregistered()
        {
            ChangeState(Constants.ROOT_STATE_NAME);
        }

        #endregion

        #region Public Methods

        public void ChangeState(string to)
        {
            if (!TryGetState(to, out var toState))
            {
                Error($"Changing state but could not find state '{to}'.");
                return;
            }

            // If we're initializing
            if (_currentState == null)
            {
                TransitionToChild(toState);
                OnStateChangeComplete?.Invoke(null, to);
                return;
            }
            
            var fromState = _currentState;

            // Get the absolute paths for the current and target states
            var fromPath = GetPath(fromState).ToList();
            var toPath = GetPath(toState).ToList();

            // Get the indices of the nearest shared parent
            int sharedParentIndexInFrom = fromPath.FindIndex(toPath.Contains);
            if (sharedParentIndexInFrom < 0)
            {
                Error($"Failed transition. Could not find common parent of {fromState.Id} and {to}.");
                return;
            }

            int sharedParentIndexInTo = toPath.FindIndex(state => state.Equals(fromPath[sharedParentIndexInFrom]));
            
            // Remove all states above and including the shared parent
            fromPath.RemoveRange(sharedParentIndexInFrom + 1, fromPath.Count - sharedParentIndexInFrom - 1);
            toPath.RemoveRange(sharedParentIndexInTo + 1, toPath.Count - sharedParentIndexInTo - 1);
            
            // Reverse the 'to' path
            toPath.Reverse();

            // Traverse through parents until we're at a child of the shared parent
            for (int i = 0; i < fromPath.Count - 1; i++)
                TransitionToParent(fromPath[i + 1]);
            
            // Traverse through children until we arrive at the target state
            for (int i = 0; i < toPath.Count - 1; i++)
                TransitionToChild(toPath[i + 1]);
            
            // Once we've arrived, invoke the state changed event
            OnStateChangeComplete?.Invoke(fromState.Id, to);
        }
        
        public async UniTask ChangeStateAsync(string to)
        {
            if (!TryGetState(to, out var toState))
            {
                Error($"Changing state but could not find state '{to}'.");
                return;
            }

            // If we're initializing
            if (_currentState == null)
            {
                await TransitionToChildAsync(toState);
                OnStateChangeComplete?.Invoke(null, to);
                return;
            }
            
            var fromState = _currentState;

            // Get the absolute paths for the current and target states
            var fromPath = GetPath(fromState).ToList();
            var toPath = GetPath(toState).ToList();

            // Get the indices of the nearest shared parent
            int sharedParentIndexInFrom = fromPath.FindIndex(toPath.Contains);
            if (sharedParentIndexInFrom < 0)
            {
                Error($"Failed transition. Could not find common parent of {fromState.Id} and {to}.");
                return;
            }

            int sharedParentIndexInTo = toPath.FindIndex(state => state.Equals(fromPath[sharedParentIndexInFrom]));
            
            // Remove all states above and including the shared parent
            fromPath.RemoveRange(sharedParentIndexInFrom + 1, fromPath.Count - sharedParentIndexInFrom - 1);
            toPath.RemoveRange(sharedParentIndexInTo + 1, toPath.Count - sharedParentIndexInTo - 1);
            
            // Reverse the 'to' path
            toPath.Reverse();

            // Traverse through parents until we're at a child of the shared parent
            for (int i = 0; i < fromPath.Count - 1; i++)
                await TransitionToParentAsync(fromPath[i + 1]);
            
            // Traverse through children until we arrive at the target state
            for (int i = 0; i < toPath.Count - 1; i++)
                await TransitionToChildAsync(toPath[i + 1]);
            
            // Once we've arrived, invoke the state changed event
            OnStateChangeComplete?.Invoke(fromState.Id, to);
        }

        public int GetChildCount(string stateId)
        {
            if (TryGetState(stateId, out var state)) 
                return state.Children.Count;
            
            Error($"No state with Id {stateId} registered.");
            return 0;
        }
        
        #endregion
        
        #region Private methods

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            var machine = new Machine();
            Locator.Register(machine);
        }
        
        private bool TryGetState(string id, out State state)
        {
            return _states.TryGetValue(id, out state);
        }

        private IEnumerable<State> GetPath(State state)
        {
            if (state == null)
                yield break;

            while (state != null)
            {
                yield return state;
                state = GetParent(state);
            }
        }

        private State GetParent(State state)
        {
            string parentId = state.Parent;
            if (string.IsNullOrEmpty(state.Parent) || !TryGetState(parentId, out var parent))
                return null;

            return parent;
        }

        private void TransitionToParent(State state)
        {
            // We're exiting a state
            _currentState.OnExit?.Invoke(_currentState.Id, state.Id);
            _currentState = state;
        }

        private void TransitionToChild(State state)
        {
            // We're entering a state
            var fromState = _currentState;
            _currentState = state;
            state.OnEnter?.Invoke(fromState, state);
        }

        private async UniTask TransitionToParentAsync(State state)
        {
            if (_currentState.OnExitAsync != null)
                await _currentState.OnExitAsync.Invoke(_currentState, state);
            else
                _currentState.OnExit?.Invoke(_currentState, state);
            
            _currentState = state;
        }

        private async UniTask TransitionToChildAsync(State state)
        {
            var fromState = _currentState;
            _currentState = state;

            if (state.OnEnterAsync != null)
                await state.OnEnterAsync.Invoke(fromState, state);
            else
                state.OnEnter?.Invoke(fromState, state);
        }

        private static void Error(string error) => Logger.Error(nameof(Machine), error);
        
        #endregion
    }
}
