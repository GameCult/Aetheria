using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public static class EditorDispatcher
{
    private static readonly Queue<Action> dispatchQueue = new Queue<Action>();
    private static double timeSliceLimit = 10.0; // in miliseconds
    private static Stopwatch timer;

    static EditorDispatcher()
    {
        EditorApplication.update += Update;
        timer = new Stopwatch();
    }
    
	private static void Update ()
    {
        lock (dispatchQueue)
        {
            int dispatchCount = 0;

            timer.Reset();
            timer.Start();

            while (dispatchQueue.Count > 0 && (timer.Elapsed.TotalMilliseconds <= timeSliceLimit))
            {
                dispatchQueue.Dequeue().Invoke();

                dispatchCount++;
            }

            timer.Stop();

//            if (dispatchCount > 0)
//            UnityEngine.Debug.Log(string.Format("[EditorDispatcher] Dispatched {0} calls in {1}ms", dispatchCount, timer.Elapsed.TotalMilliseconds));

            // todo some logic for disconnecting update when the queue is empty
        }
	}

    /// <summary>
    /// Send an Action Delegate to be run on the main thread. See EditorDispatchActions for some common usecases.
    /// </summary>
    /// <param name="task">An action delegate to run on the main thread</param>
    /// <returns>An AsyncDispatch that can be used to track if the dispatch has completed.</returns>
    public static AsyncDispatch Dispatch(Action task)
    {
        lock (dispatchQueue)
        {
            AsyncDispatch dispatch = new AsyncDispatch();
            
            // enqueue a new task that runs the supplied task and completes the dispatcher 
            dispatchQueue.Enqueue(() => { task(); dispatch.FinishedDispatch(); }); 

            return dispatch;
        }
    }
}

/// <summary>
/// Represents the progress of the dispatched action. Can be yielded to in a coroutine.
/// If not using coroutines, look at the IsDone property to find out when its okay to proceed.
/// </summary>
public class AsyncDispatch : CustomYieldInstruction
{
    public bool IsDone { get; private set; }
    public override bool keepWaiting { get { return !IsDone; } }


    /// <summary>
    /// Flags this dispatch as completed.
    /// </summary>
    internal void FinishedDispatch()
    {
        IsDone = true;
    }
}