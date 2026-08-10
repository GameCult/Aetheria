using System;
using System.Collections.Generic;
using System.Linq;
using DataStructures.ViliWonka.Heap;
using DataStructures.ViliWonka.KDTree;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using Random = Unity.Mathematics.Random;

public static class WeightedSampleElimination
{
	private static float2 boundsMin = float2.zero;			// The minimum bounds of the sampling domain
	private static float2 boundsMax = float2(1);			// The maximum bounds of the sampling domain

	// Returns the minimum bounds of the sampling domain.
	// The sampling domain boundaries are used for tiling and computing the maximum possible
	// Poisson disk radius for the sampling domain. The default boundaries are between 0 and 1.
	public static float2 BoundsMin => boundsMin;

	// Returns the maximum bounds of the sampling domain.
	// The sampling domain boundaries are used for tiling and computing the maximum possible
	// Poisson disk radius for the sampling domain. The default boundaries are between 0 and 1.
	public static float2 BoundsMax => boundsMax;

	// This is the main method that uses weighted sample elimination for selecting a subset of samples
	// with blue noise (Poisson disk) characteristics from a given input sample set (inputPoints). 
	// The selected samples are copied to outputPoints. The output size must be smaller than the input size.
	// 
	// If the progressive parameter is true, the output sample points are ordered for progressive sampling,
	// such that when the samples are introduced one by one in this order, each subset in the sequence
	// exhibits blue noise characteristics.
	// 
	// The d_max parameter defines radius within which the weight function is non-zero.
	// 
	// The weight function is the crucial component of weighted sample elimination. It computes the weight
	// of a sample point based on the placement of its neighbors within d_max radius. The weight function
	// must have the following form:
	//
	// float weightFunction( float2 p0, float2 p1, float dist2, float dmax, float density )
	//
	// The arguments p0 and p1 are the two neighboring points, dist2 is the square of the Euclidean distance 
	// between these two points, and d_max is the current radius for the weight function.
	// Note that if the progressive parameter is on, the d_max value sent to the weight function can be
	// different than the d_max value passed to this method.
	public static void Eliminate (
		float2[] inputPoints, 
		float2[] outputPoints,
		Func<float2, float2, float, float, float, float> weightFunction,
		Func<float2, float> densityFunction,
		float d_max = 0)
	{
		if ( d_max < .001f ) d_max = 2 * GetMaxPoissonDiskRadius( outputPoints.Length );
		DoEliminate( inputPoints, outputPoints, d_max, weightFunction, densityFunction );
	}

	// public static void Eliminate(
	// 	float2[] inputPoints, 
	// 	float2[] outputPoints,
	// 	float d_max = 0,
	// 	float alpha = 8,
	// 	float beta = 0.65f,
	// 	float gamma = 1.5f)
	// {
	// 	if ( d_max < .001f ) d_max = 2 * GetMaxPoissonDiskRadius( outputPoints.Length );
	// 	float d_min = d_max * GetWeightLimitFraction( inputPoints.Length, outputPoints.Length, beta, gamma );
	// 	Eliminate( inputPoints, outputPoints, (p0, p1, d2, dmax, density) => 
	// 	{
	// 		float d = sqrt(d2);
	// 		if ( d < d_min ) d = d_min;
	// 		return pow( 1f / (d / dmax + .01f), alpha );
	// 	}, v => 1, d_max);
	// }

	public static void Eliminate(
		float2[] inputPoints, 
		float2[] outputPoints,
		Func<float2, float> densityFunc = null, 
		float d_max = 0)
	{
		if (densityFunc == null) densityFunc = v => 1;
		if ( d_max < .001f ) d_max = 2 * GetMaxPoissonDiskRadius( outputPoints.Length );
		Eliminate( inputPoints, outputPoints, (p0, p1, d2, dmax, density) => 
		{
			float d = sqrt(d2);
			return pow( 1 - d / dmax, 5 + 6 * density );
		}, densityFunc, d_max);
	}

	public static float2[] GeneratePoints(int count, ref Random random, Func<float2, float> density = null, Func<float2, float> envelope = null, Action<string> progressCallback = null)
	{
		if (density == null) density = v => .5f;
		if (envelope == null) envelope = v => 1;
		var inputSamples = new float2[count * 8];
		var sample = 0;
		var accumulator = 0f;
		while (sample < inputSamples.Length)
		{
			var v = random.NextFloat2();
			accumulator += pow(saturate(density(v)), 2f) * saturate(envelope(v));
			if (accumulator > .5f)
			{
				accumulator = 0;
				inputSamples[sample++] = v;
				progressCallback?.Invoke($"Generating Samples: {sample} / {inputSamples.Length}");
			}
		}
		var outputSamples = new float2[count];
		progressCallback?.Invoke("Eliminating Samples");
		Eliminate(inputSamples, outputSamples, density);
		return outputSamples;
	}

	// Returns the maximum possible Poisson disk radius in the given dimensions for the given sampleCount
	// to spread over the given domainSize. If the domainSize argument is zero or negative, it is computed
	// as the area or N-dimensional volume of the box defined by the minimum and maximum bounds.
	// This method is used for the default weight function.
	static float GetMaxPoissonDiskRadius( int sampleCount )
	{
		var domainSize = boundsMax[0] - boundsMin[0];
		domainSize *= domainSize;
		float sampleArea = domainSize / sampleCount;
		return sqrt( sampleArea / ( 2 * sqrt(3) ) );
	}

	// This is the method that performs weighted sample elimination.
	static void DoEliminate( 
		float2[]         inputPoints, 
		float2[]         outputPoints, 
		float            d_max,
		Func<float2, float2, float, float, float, float>   weightFunction,
		Func<float2, float> density
		)
	{
		// Build a k-d tree for samples
		var kdtree = new KDTree(inputPoints);
		var query = new KDQuery();

		var dmax2 = d_max * d_max;
		// Assign weights to each sample
		float[] weights = new float[inputPoints.Length];
		float[] densities = new float[inputPoints.Length];
		for (int i = 0; i < inputPoints.Length; i++)
		{
			var point = inputPoints[i];
			densities[i] += density(point);
			foreach (var (qi, d2) in query.Radius(kdtree, inputPoints[i], d_max))
			{
				var otherPoint = inputPoints[qi];
				weights[i] += weightFunction(point, otherPoint, d2, d_max, densities[i]);
			}
		}

		// Build a heap for the samples using their weights
		MaxHeap<int> heap = new MaxHeap<int>(inputPoints.Length);
		for (int i = 0; i < inputPoints.Length; i++)
			heap.PushObj(i, weights[i]);

		// While the number of samples is greater than desired
		int sampleCount = inputPoints.Length;
		while (sampleCount > outputPoints.Length)
		{
			// Pull the top sample from heap
			var elim = heap.PopObj();
			var pElim = inputPoints[elim];
			// For each sample around it, remove its weight contribution and update the heap
			foreach (var (qi, d2) in query.Radius(kdtree, pElim, d_max))
			{
				var point = inputPoints[qi];
				weights[qi] -= weightFunction(point, pElim, d2, d_max, densities[qi]);
				heap.SetValue(qi, weights[qi]);
			}
			sampleCount--;
		}

		var outputIndex = 0;
		foreach (var i in heap.FlushResult())
		{
			outputPoints[outputIndex++] = inputPoints[i];
		}
	}

	// Returns the minimum radius fraction used by the default weight function.
	static float GetWeightLimitFraction( int inputSize, int outputSize, float beta, float gamma )
	{
		float ratio = (float) outputSize / inputSize;
		return ( 1 - pow( ratio, gamma ) ) * beta;
	}
}