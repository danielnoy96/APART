using System.Reflection;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace Game.GOAP
{
    [DefaultExecutionOrder(-200)]
    public class GoapRunnerResolver : MonoBehaviour
    {
        [Tooltip("Optional explicit runner reference (useful for scene-only enemies). Leave empty on prefabs.")]
        [SerializeField] private GoapBehaviour runner;

        private static readonly FieldInfo AgentTypeRunnerField =
            typeof(AgentTypeBehaviour).GetField("runner", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Awake()
        {
            if (runner == null)
                runner = FindAnyObjectByType<GoapBehaviour>();

            if (runner == null)
            {
                Debug.LogError("No GoapBehaviour found in scene. Add a GOAP GameObject with GoapBehaviour + ReactiveControllerBehaviour.", this);
                return;
            }

            var agentTypeBehaviour = GetComponent<AgentTypeBehaviour>();
            if (agentTypeBehaviour == null)
                return;

            if (AgentTypeRunnerField == null)
            {
                Debug.LogError("Failed to reflect AgentTypeBehaviour.runner field (GOAP package API changed).", this);
                return;
            }

            var current = AgentTypeRunnerField.GetValue(agentTypeBehaviour) as GoapBehaviour;
            if (current == null)
                AgentTypeRunnerField.SetValue(agentTypeBehaviour, runner);
        }
    }
}

