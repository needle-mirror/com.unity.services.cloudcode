using __CLOUD_NAMESPACE__;
using Unity.Services.CloudBehaviours;
using UnityEngine;

namespace __NAMESPACE__
{
    [CloudBehaviour(typeof(global::__CLOUD_NAMESPACE__.BehaviourModuleCloudTemplate))]
    public partial class BehaviourModuleClientTemplate : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            EnableClient();
        }

        protected virtual void OnDisable()
        {
            DisableClient();
        }
    }
}
