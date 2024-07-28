using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Services;
using UnityEngine;
using UnityEngine.TestTools;

namespace StateMachine
{
    public class StateMachineTests
    {
        private const int TEST_PRIORITY = int.MinValue;
        private const float TIMEOUT = 5f;
        
        /*
         * ###### Structure ######
         * 
         * ROOT_STATE:  A
         * A:           A_1     A_2     B
         * B:           B_1     C
         * C:           C_1
         * 
         */

        private const string A_NAME = "TEST STATE: A";
        private const string A_1_NAME = "TEST STATE: A_1";
        private const string A_2_NAME = "TEST STATE: A_2";
        
        private const string B_NAME = "TEST STATE: B";
        private const string B_1_NAME = "TEST STATE: B_1";

        private const string C_NAME = "TEST STATE: C";
        private const string C_1_NAME = "TEST STATE: C_1";

        private Machine _machine;

        #region Test Methods

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (!Locator.Get(out _machine))
            {
                _machine = new Machine();
                Locator.Register(_machine);
            }
            
            float timeoutTime = Time.realtimeSinceStartup + TIMEOUT;
            yield return new WaitUntil(() => _machine.Initialized || Time.realtimeSinceStartup > timeoutTime);

            Assert.IsTrue(_machine.Initialized, $"Machine failed to be initialized before the {TIMEOUT} second timeout.");
        }

        [UnityTest]
        public IEnumerator StateChangeTest()
        {
            // Make sure we're on A_1
            AssertChange(A_1_NAME);
            
            // A_1 -> A_2
            AssertChange(A_2_NAME);

            // A_2 -> A_1
            AssertChange(A_1_NAME);
            
            // A_1 -> B
            AssertChange(B_NAME);
            
            // B_1 -> C_1 (directly)
            AssertChange(C_1_NAME);
            
            // C_1 -> A
            AssertChange(A_NAME);
            
            // A_1 -> C
            AssertChange(C_NAME);

            yield break;

            void AssertChange(string id)
            {
                _machine.ChangeState(id);
                Assert.AreEqual(id, _machine.CurrentStateId);
            }
        }

        [UnityTest]
        public IEnumerator StateChangeAsyncTest()
        {
            // Make sure we're on A_1
            yield return AssertChange(A_1_NAME);
            
            // A_1 -> A_2
            yield return AssertChange(A_2_NAME);

            // A_2 -> A_1
            yield return AssertChange(A_1_NAME);
            
            // A_1 -> B
            yield return AssertChange(B_NAME);
            
            // B_1 -> C_1 (directly)
            yield return AssertChange(C_1_NAME);
            
            // C_1 -> A
            yield return AssertChange(A_NAME);
            
            // A_1 -> C
            yield return AssertChange(C_NAME);

            yield break;

            IEnumerator AssertChange(string id)
            {
                string fromState = _machine.CurrentStateId;
                
                var changeTask = _machine.ChangeStateAsync(id);
                var awaiter = changeTask.GetAwaiter();

                yield return null;
                Assert.AreNotEqual(id, _machine.CurrentStateId, $"Current state is {id} sooner than expected!");
                
                yield return new WaitUntil(() => awaiter.IsCompleted);
                
                Assert.AreEqual(id, _machine.CurrentStateId, $"Did not end up on {id} after async changing from {fromState}");
            }
        }

        [UnityTest]
        public IEnumerator StructureTest()
        {
            // Ensure 'A' has 3 children - A_1, A_2 and B
            Assert.AreEqual(3, _machine.GetChildCount(A_NAME));
            
            // Ensure 'B' has 2 children - B_1 and C
            Assert.AreEqual(2, _machine.GetChildCount(B_NAME));
            
            // Ensure 'C' has 1 child - C_1
            Assert.AreEqual(1, _machine.GetChildCount(C_NAME));

            yield break;
        }
        
        #endregion
        
        #region Private Methods
        
        #region State Machine Constructors

        [ConstructStateMachine]
        private static void ConstructStateMachineA(StateConstructor baseState)
        {
            // Create state 'A'
            var aState = new StateConstructor(A_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync);
            aState.AddState(new StateConstructor(A_1_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync));
            aState.AddState(new StateConstructor(A_2_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync));
        
            // Add to the base state
            baseState.AddState(aState);
        }
        
        [ConstructStateMachine(A_NAME, TEST_PRIORITY)]
        private static void ConstructStateMachineB(StateConstructor baseState)
        {
            var bState = new StateConstructor(B_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync);
            bState.AddState(new StateConstructor(B_1_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync));
            
            baseState.AddState(bState);
        }
        
        [ConstructStateMachine(B_NAME, TEST_PRIORITY)]
        private static void ConstructStateMachineC(StateConstructor baseState)
        {
            var state = new StateConstructor(C_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync);
            state.AddState(new StateConstructor(C_1_NAME, OnEnter, OnExit, OnEnterAsync, OnExitAsync));
        
            baseState.AddState(state);
        }
        
        #endregion
        
        #region State Listeners

        private static void OnEnter(string from, string to)
        {
            Debug.Log(EnterLog(from, to));
        }

        private static void OnExit(string from, string to)
        {
            Debug.Log(ExitLog(from, to));
        }

        private static async UniTask OnEnterAsync(string from, string to)
        {
            Debug.Log(EnterLog(from, to));
            Debug.Log("Waiting one second...");
            await UniTask.Delay(1000);
        }

        private static async UniTask OnExitAsync(string from, string to)
        {
            Debug.Log(ExitLog(from, to));
            Debug.Log("Waiting one second...");
            await UniTask.Delay(1000);
        }
        
        #endregion
        
        #region Utility

        private static string EnterLog(string from, string to)
        {
            return $"OnEnter: {from} -> {to}";
        }

        private static string ExitLog(string from, string to)
        {
            return $"OnExit: {from} -> {to}";
        }
        
        #endregion
        
        #endregion
    }
}
