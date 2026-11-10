using UnityEngine;

namespace Sample
{
    /// <summary>Composition root: Contexts + systems; Visual Debugging in the editor.</summary>
    public sealed class SampleRoot : MonoBehaviour, IAnyHealthListener
    {
        Contexts _contexts;
        Entitas.Systems _systems;

        void Awake()
        {
            _contexts = new Contexts();
#if UNITY_EDITOR
            foreach (var context in _contexts.allContexts)
            {
                var observer = new Entitas.VisualDebugging.Unity.ContextObserver(context);
                if (Application.isPlaying) Object.DontDestroyOnLoad(observer.gameObject);
            }
#endif
            _systems = new Feature()
                .Add(new MoveSystem(_contexts))
                .Add(new GameEventSystems(_contexts))
                .Add(new InputCleanupSystems(_contexts));
            _systems.Initialize();

            _contexts.game.SetScore(0);
            var player = _contexts.game.CreateEntity();
            player.AddPlayerId(1);
            player.AddPosition(0f, 0f);
            player.AddHealth(3);
            player.isAlive = true;
            _contexts.game.CreateEntity().AddAnyHealthListener(this);
        }

        void Update()
        {
            // Intent: move player 1 right (no input backend dependency in the sample).
            _contexts.input.CreateEntity().AddMoveInput(1, Time.deltaTime, 0f);

            _systems.Execute();
            _systems.Cleanup();
        }

        void OnDestroy()
        {
            _systems?.TearDown();
            _contexts?.Reset();
        }

        public void OnAnyHealth(GameEntity entity, int value) => Debug.Log($"Health changed: {value}");
    }
}