using UnityEngine;

public class movement : MonoBehaviour
{
    [SerializeField] private float _speed = 2.0f;

    // Update is called once per frame
    void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        transform.Translate(Vector3.right * (horizontalInput * _speed * Time.deltaTime));
        transform.Translate(Vector3.up * (verticalInput * _speed * Time.deltaTime));
    }
}
