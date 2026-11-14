using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : MonoBehaviour
{
    public class Marker
    {
        public Vector3 position;
        public Quaternion rotation;

        public Marker (Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }

    public List<Marker> markerList = new List<Marker>();
    [SerializeField] AnimationCurveVariable offsetValueCurve;
    [SerializeField] float cartMaxSpeed = 30f;
    [SerializeField] float offsetValue = 0.0f;

    void Start()
    {
        offsetValueCurve = Resources.Load<AnimationCurveVariable>("SO_Variables/Offset Value Curve");
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        UpdateMarkerList();
    }

    public void UpdateMarkerList()
    {
        markerList.Add(new Marker(transform.position + -1 * offsetValue * transform.forward, transform.rotation));
        //float dist = Vector3.Distance(transform.position, lastMarkerPosition);
        //distanceSinceLastMarker += dist;

        //if (distanceSinceLastMarker >= markerSpacing)
        //{
        //    markerList.Add(new Marker(transform.position, transform.rotation));
        //    lastMarkerPosition = transform.position;
        //    distanceSinceLastMarker = 0f;
        //}
    }

    public void ClearMarkerList()
    {
        markerList.Clear();
        markerList.Add(new Marker(transform.position + -1 * offsetValue * transform.forward, transform.rotation));
        //markerList.Clear();
        //lastMarkerPosition = transform.position;
        //distanceSinceLastMarker = 0f;
        //markerList.Add(new Marker(transform.position, transform.rotation));
    }
}
