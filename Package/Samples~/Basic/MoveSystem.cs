using System.Collections.Generic;
using Entitas;

namespace Sample
{
    /// <summary>Applies move intents (Input context) to players (Game context) found by primary index.</summary>
    public sealed class MoveSystem : ReactiveSystem<InputEntity>
    {
        readonly Contexts _contexts;

        public MoveSystem(Contexts contexts) : base(contexts.input) => _contexts = contexts;

        protected override ICollector<InputEntity> GetTrigger(IContext<InputEntity> context)
            => context.CreateCollector(InputMatcher.MoveInput);

        protected override bool Filter(InputEntity entity) => entity.hasMoveInput;

        protected override void Execute(List<InputEntity> entities)
        {
            foreach (var input in entities)
            {
                var player = _contexts.game.GetEntityWithPlayerId(input.moveInput.PlayerId);
                if (player == null || !player.isAlive) continue;
                var p = player.position;
                player.ReplacePosition(p.X + input.moveInput.Dx, p.Y + input.moveInput.Dy);
            }
        }
    }
}