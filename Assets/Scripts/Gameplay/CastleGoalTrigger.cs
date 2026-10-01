using System;
using Reins;
using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider),typeof(Rigidbody))]
    public sealed class CastleGoalTrigger : MonoBehaviour
    {
        [SerializeField] private CarriageMotor carriage;
        private BoxCollider zone;
        public bool HasArrived {get; private set;}
        public event Action CartArrived;
        private void Awake()
        {
            zone=GetComponent<BoxCollider>(); zone.isTrigger=true;
            var body=GetComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
        }
        public bool ContainsCarriagePosition(Vector3 worldPosition)
        {
            if(zone==null) zone=GetComponent<BoxCollider>();
            var p=transform.InverseTransformPoint(worldPosition)-zone.center;
            var half=zone.size*.5f;
            return Mathf.Abs(p.x)<=half.x && Mathf.Abs(p.y)<=half.y && Mathf.Abs(p.z)<=half.z;
        }
        private void OnTriggerEnter(Collider other) => TryAccept(other);
        private void OnTriggerStay(Collider other) => TryAccept(other);
        public bool TryAccept(Collider other)
        {
            if(HasArrived || carriage==null || other==null || other.GetComponentInParent<CarriageMotor>()!=carriage ||
                !ContainsCarriagePosition(carriage.transform.position)) return false;
            var session=FindAnyObjectByType<GameSessionController>();
            if(session==null || session.Phase!=RideSessionPhase.Playing) return false;
            HasArrived=true;
            CartArrived?.Invoke();
            return true;
        }
    }
}
