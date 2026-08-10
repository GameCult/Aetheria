/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

//Generates random names based on the statistical weight of letter sequences
//in a collection of sample names
public class MarkovNameGenerator
{
    private Dictionary<string, List<char>> _chains = new Dictionary<string, List<char>>();
    private List<string> _samples = new List<string>();
    private static List<string> _used = new List<string>();
    private int _order;
    private int _minLength;
    private int _maxLength;
    private Random _random;

    public MarkovNameGenerator(ref Random random, IEnumerable<string> sampleNames, NameGeneratorSettings settings)
    {
        var names = new HashSet<string>();
        foreach (var line in sampleNames)
        {
            foreach(var word in line.ToUpperInvariant().Split(' ', ',', '.', '"'))
                if (word.Length >= settings.NameGeneratorMinLength && !names.Contains(word))
                    names.Add(word);
        }
        
        _random = random;

        _order = max(1, settings.NameGeneratorOrder);
        _minLength = max(1, settings.NameGeneratorMinLength);
        _maxLength = settings.NameGeneratorMaxLength;
        
        Initialize(names);
    }
    
    public MarkovNameGenerator(ref Random random, IEnumerable<string> sampleNames, int order, int minLength, int maxLength)
    {
        _random = random;

        _order = max(1, order);
        _minLength = max(1, minLength);
        _maxLength = maxLength;
        
        Initialize(sampleNames);
    }

    private void Initialize(IEnumerable<string> sampleNames)
    {
        //split comma delimited lines
        foreach (string line in sampleNames)
        {
            string[] tokens = line.Split(' ','\'','(',')');
            foreach (string word in tokens)
            {
                string upper = word.Trim().ToLowerInvariant();
                if (upper.Length < _order + 1)
                    continue;                   
                _samples.Add(upper + '|'); // Add end character
            }
        }

        //Build chains            
        foreach (string word in _samples)
        {              
            for (int letter = 0; letter < word.Length - _order; letter++)
            {
                string token = word.Substring(letter, _order);
                List<char> entry = null;
                if (_chains.ContainsKey(token))
                    entry = _chains[token];
                else
                {
                    entry = new List<char>();
                    _chains[token] = entry;
                }
                entry.Add(word[letter + _order]);
            }
        }
    }

    //Get the next random name
    public string NextName
    {
        get
        {
                            
            string s = "";
            do
            {
                int n = _random.NextInt(0,_samples.Count);
                //int nameLength = _samples[n].Length;
                s = _samples[n].Substring(0, _order);//get a random token at the start of a sample word
                while (true)//(s.Length < nameLength)
                {
                    string token = s.Substring(s.Length - _order, _order);
                    char c = GetLetter(token);
                    if (c != '?' && c != '|')
                        s += c;//GetLetter(token);
                    else
                        break;
                }
                s = s.Substring(0, 1).ToUpper() + s.Substring(1);
            }
            while (_used.Contains(s) || s.Length < _minLength || s.Length > _maxLength);
            _used.Add(s);
            return s;
        }
    }

    //Reset the used names
    public void Reset()
    {
        _used.Clear();
    }

    //Get a random letter from the chain
    private char GetLetter(string token)
    {
        if (!_chains.ContainsKey(token))
            return '?';
        List<char> letters = _chains[token];
        int n = _random.NextInt(0,letters.Count);
        return letters[n];
    }
}