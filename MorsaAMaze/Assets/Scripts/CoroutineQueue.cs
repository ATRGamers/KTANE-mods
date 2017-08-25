﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoroutineQueue : MonoBehaviour
{
    private void Awake()
    {
        _coroutineQueue = new Queue<IEnumerator>();
    }

    private void Update()
    {
        if (!Processing && _coroutineQueue.Count > 0)
        {
            Processing = true;
            _activeCoroutine = StartCoroutine(ProcessQueueCoroutine());
        }
    }

    public void AddToQueue(IEnumerator subcoroutine)
    {
        _coroutineQueue.Enqueue(subcoroutine);
    }

    public void CancelFutureSubcoroutines()
    {
        _coroutineQueue.Clear();
    }

    public void StopQueue()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }

        Processing = false;
    }

    private IEnumerator ProcessQueueCoroutine()
    {

        while (_coroutineQueue.Count > 0)
        {
            IEnumerator coroutine = _coroutineQueue.Dequeue();
            while (coroutine.MoveNext())
            {
                yield return coroutine.Current;
            }
        }

        Processing = false;
        _activeCoroutine = null;
    }

    public bool Processing { get; private set; }

    private Queue<IEnumerator> _coroutineQueue = null;
    private Coroutine _activeCoroutine = null;

    public CoroutineQueue()
    {
        Processing = false;
    }
}
