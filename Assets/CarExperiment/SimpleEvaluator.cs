using UnityEngine;
using System.Collections;
using SharpNeat.Core;
using SharpNeat.Phenomes;
using System.Collections.Generic;

public class SimpleEvaluator : IPhenomeEvaluator<IBlackBox> {

	ulong _evalCount;
    bool _stopConditionSatisfied;
    Optimizer optimizer;
    FitnessInfo fitness;

    Dictionary<IBlackBox, FitnessInfo> dict = new Dictionary<IBlackBox, FitnessInfo>();

    public ulong EvaluationCount
    {
        get { return _evalCount; }
    }

    public bool StopConditionSatisfied
    {
        get { return _stopConditionSatisfied; }
    }

    public SimpleEvaluator(Optimizer se)
    {
        this.optimizer = se;
    }

    public IEnumerator Evaluate(IBlackBox box)
    {
        if (optimizer != null)
        {
            float totalFitness = 0f;
            for (int i = 0; i < optimizer.Trials; i++)
            {
                optimizer.Evaluate(box);
                yield return new WaitForSeconds(optimizer.TrialDuration);
                float fit = optimizer.GetFitness(box);
                totalFitness += fit;
                optimizer.StopEvaluation(box);
                Debug.Log($"Trial {i+1} fitness: {fit}");
            }
            float avgFitness = totalFitness / optimizer.Trials;
            FitnessInfo fitness = new FitnessInfo(avgFitness, avgFitness);
            dict.Add(box, fitness);
            Debug.Log($"Average fitness for genome: {avgFitness}");
        }
    }

    public void Reset()
    {
        this.fitness = FitnessInfo.Zero;
        dict = new Dictionary<IBlackBox, FitnessInfo>();
    }

    public FitnessInfo GetLastFitness()
    {
        
        return this.fitness;
    }


    public FitnessInfo GetLastFitness(IBlackBox phenome)
    {
        if (dict.ContainsKey(phenome))
        {
            FitnessInfo fit = dict[phenome];
            dict.Remove(phenome);
           
            return fit;
        }
        
        return FitnessInfo.Zero;
    }
}