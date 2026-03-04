using Ggj2026.Game.Shared.Ids;
using UnityEngine;

namespace Ggj2026.Game.Presentation
{
    public class WorldEntityPresentationBridge : MonoBehaviour
    {
        [SerializeField] private ulong entityUid;

        public EntityUid EntityUid => new EntityUid(entityUid);
    }
}
