using System.Collections.Generic;
using UnityEngine;


    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [SerializeField] private float speed = 2f;
        [Tooltip("Pause (in seconds) at each end point before turning around")]
        [SerializeField] private float waitTime = 0.5f;

        private Rigidbody2D m_rb;
        private Vector2 m_target;
        private float m_waitTimer;
        private readonly HashSet<Rigidbody2D> m_riders = new HashSet<Rigidbody2D>();

        private void Awake()
        {
            m_rb = GetComponent<Rigidbody2D>();
            m_rb.bodyType = RigidbodyType2D.Kinematic;
            m_rb.useFullKinematicContacts = true;
        }

        private void Start()
        {
            m_target = pointB.position;
        }

        private void FixedUpdate()
        {
            if (m_waitTimer > 0f)
            {
                m_waitTimer -= Time.fixedDeltaTime;
                return;
            }

            Vector2 current = m_rb.position;
            Vector2 next = Vector2.MoveTowards(current, m_target, speed * Time.fixedDeltaTime);
            Vector2 delta = next - current;

            m_rb.MovePosition(next);

            // carry every rider by the same amount the platform moved this step,
            // so the controllers' velocity-based movement stays untouched
            foreach (Rigidbody2D rider in m_riders)
            {
                if (rider != null)
                    rider.position += delta;
            }

            if (Vector2.Distance(next, m_target) < 0.01f)
            {
                m_target = ((Vector2)m_target == (Vector2)pointA.position)
                    ? (Vector2)pointB.position
                    : (Vector2)pointA.position;
                m_waitTimer = waitTime;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryAttach(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!m_riders.Contains(collision.rigidbody))
                TryAttach(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.rigidbody != null)
                m_riders.Remove(collision.rigidbody);
        }

        private void TryAttach(Collision2D collision)
        {
            if (collision.rigidbody == null || collision.rigidbody.bodyType != RigidbodyType2D.Dynamic)
                return;

            // only attach if the object is standing on top, not bumping the side/bottom
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < -0.5f)
                {
                    m_riders.Add(collision.rigidbody);
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (pointA != null && pointB != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pointA.position, pointB.position);
                Gizmos.DrawWireSphere(pointA.position, 0.15f);
                Gizmos.DrawWireSphere(pointB.position, 0.15f);
            }
        }
    }