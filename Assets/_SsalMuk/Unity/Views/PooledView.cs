using UnityEngine;
namespace SsalMuk.Unity
{
    public abstract class PooledView : MonoBehaviour
    {
        public LeaseToken Lease { get; private set; }
        public void BindLease(LeaseToken token) { Lease = token; }
        public bool Accepts(LeaseToken token) => token.Generation > 0 && Lease.Equals(token);
        public virtual void ResetVisuals()
        { Lease = default; transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one; }
    }
}
