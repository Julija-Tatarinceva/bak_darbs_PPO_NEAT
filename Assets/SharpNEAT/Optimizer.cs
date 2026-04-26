using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;
using System.Collections.Generic;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System;
using System.Xml;
using System.IO;
using CarExperiment;

public class Optimizer : MonoBehaviour {

    const int NUM_INPUTS = 5;
    const int NUM_OUTPUTS = 2;

    public int Trials;
    public float TrialDuration;
    public float StoppingFitness;
    bool EARunning;
    string popFileSavePath, champFileSavePath;

    SimpleExperiment experiment;
    static NeatEvolutionAlgorithm<NeatGenome> _ea;

    public GameObject Unit;
    public TrackGenerator trackGenerator;

    Dictionary<IBlackBox, UnitController> ControllerMap = new Dictionary<IBlackBox, UnitController>();
    private DateTime startTime;
    private float timeLeft;
    private float accum;
    private int frames;
    private float updateInterval = 12;

    private uint Generation;
    private double Fitness;

	// Use this for initialization
	void Start () {
        Utility.DebugLog = true;
        experiment = new SimpleExperiment();
        XmlDocument xmlConfig = new XmlDocument();
        TextAsset textAsset = (TextAsset)Resources.Load("experiment.config");
        xmlConfig.LoadXml(textAsset.text);
        experiment.SetOptimizer(this);
        Debug.Log("Initializing experiment" + xmlConfig.DocumentElement.OuterXml);

        experiment.Initialize("Car Experiment", xmlConfig.DocumentElement, NUM_INPUTS, NUM_OUTPUTS);

        champFileSavePath = Application.persistentDataPath + string.Format("/{0}.champ.xml", "car");
        popFileSavePath = Application.persistentDataPath + string.Format("/{0}.pop.xml", "car");       

        print(champFileSavePath);
	}

    // Update is called once per frame
    void Update()
    {
      //  evaluationStartTime += Time.deltaTime;

        timeLeft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        ++frames;
        //Debug.Log("Time left: " + timeLeft + ", Accum: " + accum + ", Frames: " + frames);

        if (timeLeft <= 0.0)
        {
            var fps = accum / frames;
            timeLeft = updateInterval;
            accum = 0.0f;
            frames = 0;
            //   print("FPS: " + fps);
            if (fps < 10)
            {
                Time.timeScale = Time.timeScale - 1;
                print("Lowering time scale to " + Time.timeScale);
            }
        }
    }

    public void StartEA()
    {        
        Utility.DebugLog = true;
        Utility.Log("Starting PhotoTaxis experiment");
        // print("Loading: " + popFileLoadPath);
        _ea = experiment.CreateEvolutionAlgorithm(popFileSavePath);
        startTime = DateTime.Now;

        _ea.UpdateEvent += new EventHandler(ea_UpdateEvent);
        _ea.PausedEvent += new EventHandler(ea_PauseEvent);

        var evoSpeed = 1;

     //   Time.fixedDeltaTime = 0.045f;
        Time.timeScale = evoSpeed;       
        _ea.StartContinue();
        EARunning = true;
    }

    void ea_UpdateEvent(object sender, EventArgs e)
    {
        Utility.Log(string.Format("gen={0:N0} bestFitness={1:N6}",
            _ea.CurrentGeneration, _ea.Statistics._maxFitness));

        Fitness = _ea.Statistics._maxFitness;
        Generation = _ea.CurrentGeneration;
    }

    void ea_PauseEvent(object sender, EventArgs e)
    {
        Time.timeScale = 1;
        Utility.Log("Done ea'ing (and neat'ing)");

        XmlWriterSettings _xwSettings = new XmlWriterSettings();
        _xwSettings.Indent = true;
        // Save genomes to xml file.        
        DirectoryInfo dirInf = new DirectoryInfo(Application.persistentDataPath);
        if (!dirInf.Exists)
        {
            Debug.Log("Creating subdirectory");
            dirInf.Create();
        }
        using (XmlWriter xw = XmlWriter.Create(popFileSavePath, _xwSettings))
        {
            experiment.SavePopulation(xw, _ea.GenomeList);
        }
        // Also save the best genome

        using (XmlWriter xw = XmlWriter.Create(champFileSavePath, _xwSettings))
        {
            experiment.SavePopulation(xw, new NeatGenome[] { _ea.CurrentChampGenome });
        }
        DateTime endTime = DateTime.Now;
        Utility.Log("Total time elapsed: " + (endTime - startTime));

        System.IO.StreamReader stream = new System.IO.StreamReader(popFileSavePath);
       

      
        EARunning = false;        
        
    }

    public void StopEA()
    {

        if (_ea != null && _ea.RunState == SharpNeat.Core.RunState.Running)
        {
            _ea.Stop();
        }
    }

    public void Evaluate(IBlackBox box)
    {
        if (trackGenerator != null)
        {
            trackGenerator.GenerateNewTrack();
        }
        GameObject obj = Instantiate(Unit, Unit.transform.position, Unit.transform.rotation) as GameObject;
        UnitController controller = obj.GetComponent<UnitController>();

        ControllerMap.Add(box, controller);

        controller.Activate(box);
    }

    public void StopEvaluation(IBlackBox box)
    {
        UnitController ct = ControllerMap[box];

        Destroy(ct.gameObject);
    }

    public void VisualizeNetwork()
    {
        NeatGenome genome = null;

        // Load the champion genome
        try
        {
            using (XmlReader xr = XmlReader.Create(champFileSavePath))
                genome = NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, (NeatGenomeFactory)experiment.CreateGenomeFactory())[0];
        }
        catch (Exception e)
        {
            Debug.LogError("Error loading champion genome: " + e.Message);
            return;
        }

        // Generate DOT format for GraphViz
        string dotFilePath = Application.persistentDataPath + "/network.dot";
        using (StreamWriter writer = new StreamWriter(dotFilePath))
        {
            writer.WriteLine("digraph NeatNetwork {");
            writer.WriteLine("  rankdir=LR;");
            writer.WriteLine("  node [shape=circle];");
            
            // Input nodes
            writer.WriteLine("  subgraph cluster_inputs {");
            writer.WriteLine("    label=\"Inputs\";");
            for (int i = 0; i < NUM_INPUTS; i++)
            {
                writer.WriteLine($"    input{i} [label=\"I{i}\", style=filled, fillcolor=lightblue];");
            }
            writer.WriteLine("  }");
            
            // Output nodes
            writer.WriteLine("  subgraph cluster_outputs {");
            writer.WriteLine("    label=\"Outputs\";");
            for (int i = 0; i < NUM_OUTPUTS; i++)
            {
                writer.WriteLine($"    output{i} [label=\"O{i}\", style=filled, fillcolor=lightgreen];");
            }
            writer.WriteLine("  }");
            
            // Hidden nodes
            writer.WriteLine("  subgraph cluster_hidden {");
            writer.WriteLine("    label=\"Hidden\";");
            HashSet<uint> hiddenNodes = new HashSet<uint>();
            foreach (var conn in genome.ConnectionGeneList)
            {
                if (conn.SourceNodeId >= NUM_INPUTS && conn.SourceNodeId < genome.NodeList.Count - NUM_OUTPUTS)
                    hiddenNodes.Add(conn.SourceNodeId);
                if (conn.TargetNodeId >= NUM_INPUTS && conn.TargetNodeId < genome.NodeList.Count - NUM_OUTPUTS)
                    hiddenNodes.Add(conn.TargetNodeId);
            }
            foreach (var nodeId in hiddenNodes)
            {
                writer.WriteLine($"    hidden{nodeId} [label=\"H{nodeId}\", style=filled, fillcolor=lightyellow];");
            }
            writer.WriteLine("  }");
            
            // Connections
            foreach (var conn in genome.ConnectionGeneList)
            {
                string sourceNode = conn.SourceNodeId < NUM_INPUTS ? $"input{conn.SourceNodeId}" : 
                                   conn.SourceNodeId >= genome.NodeList.Count - NUM_OUTPUTS ? $"output{conn.SourceNodeId - (genome.NodeList.Count - NUM_OUTPUTS)}" :
                                   $"hidden{conn.SourceNodeId}";
                
                string targetNode = conn.TargetNodeId < NUM_INPUTS ? $"input{conn.TargetNodeId}" :
                                   conn.TargetNodeId >= genome.NodeList.Count - NUM_OUTPUTS ? $"output{conn.TargetNodeId - (genome.NodeList.Count - NUM_OUTPUTS)}" :
                                   $"hidden{conn.TargetNodeId}";
                
                string color = conn.Weight > 0 ? "green" : "red";
                string style = conn.Weight > 0 ? "solid" : "dashed";
                writer.WriteLine($"  {sourceNode} -> {targetNode} [label=\"{conn.Weight:F2}\", color={color}, style={style}, penwidth={Math.Abs(conn.Weight) + 0.5}];");
            }
            
            writer.WriteLine("}");
        }
        
        Debug.Log($"Network visualization saved to: {dotFilePath}");
        Debug.Log($"Nodes: {genome.NodeList.Count}, Connections: {genome.ConnectionGeneList.Count}");
        Debug.Log("Open this file with GraphViz or online at: https://dreampuf.github.io/GraphvizOnline/");
    }

    public void RunBest()
    {
        Time.timeScale = 1;

        NeatGenome genome = null;


        // Try to load the genome from the XML document.
        try
        {
            using (XmlReader xr = XmlReader.Create(champFileSavePath))
                genome = NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, (NeatGenomeFactory)experiment.CreateGenomeFactory())[0];


        }
        catch (Exception e1)
        {
            // print(champFileLoadPath + " Error loading genome from file!\nLoading aborted.\n"
            //						  + e1.Message + "\nJoe: " + champFileLoadPath);
            return;
        }

        // Get a genome decoder that can convert genomes to phenomes.
        var genomeDecoder = experiment.CreateGenomeDecoder();

        // Decode the genome into a phenome (neural network).
        var phenome = genomeDecoder.Decode(genome);

        GameObject obj = Instantiate(Unit, Unit.transform.position, Unit.transform.rotation) as GameObject;
        UnitController controller = obj.GetComponent<UnitController>();

        ControllerMap.Add(phenome, controller);

        controller.Activate(phenome);
    }

    public float GetFitness(IBlackBox box)
    {
        if (ControllerMap.ContainsKey(box))
        {
            return ControllerMap[box].GetFitness();
        }
        return 0;
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 100, 40), "Start EA"))
        {
            StartEA();
        }
        if (GUI.Button(new Rect(10, 60, 100, 40), "Stop EA"))
        {
            StopEA();
        }
        if (GUI.Button(new Rect(10, 110, 100, 40), "Run best"))
        {
            RunBest();
        }
        if (GUI.Button(new Rect(10, 160, 100, 40), "Visualize Net"))
        {
            VisualizeNetwork();
        }

        GUI.Button(new Rect(10, Screen.height - 70, 100, 60), string.Format("Generation: {0}\nFitness: {1:0.00}", Generation, Fitness));
    }
}