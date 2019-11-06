﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BlobHandles;

namespace OscCore
{
    enum AddressType
    {
        Invalid,
        Pattern,
        Address
    }

    public sealed class OscAddressSpace
    {
        const int k_DefaultPatternCapacity = 8;
        const int k_DefaultCapacity = 16;

        internal readonly OscAddressMethods AddressToMethod;
        
        // Keep a list of registered address patterns and the methods they're associated with just like addresses
        internal int PatternCount;
        internal Regex[] Patterns = new Regex[k_DefaultPatternCapacity];
        internal OscActionPair[] PatternMethods = new OscActionPair[k_DefaultPatternCapacity];
        
        readonly Queue<int> FreedPatternIndices = new Queue<int>();
        readonly Dictionary<string, int> PatternStringToIndex = new Dictionary<string, int>();

        public OscAddressSpace(int startingCapacity = k_DefaultCapacity)
        {
            AddressToMethod = new OscAddressMethods(startingCapacity);
        }

        public bool TryAddMethod(string address, OscActionPair onReceived)
        {
            if (string.IsNullOrEmpty(address) || onReceived == null) 
                return false;

            switch (OscParser.GetAddressType(address))
            {    
                case AddressType.Address:
                    AddressToMethod.Add(address, onReceived);
                    return true;
                case AddressType.Pattern:
                    int index;
                    // if a method has already been registered for this pattern, add the new delegate
                    if (PatternStringToIndex.TryGetValue(address, out index))
                    {
                        PatternMethods[index] += onReceived;
                        return true;
                    }

                    if (FreedPatternIndices.Count > 0)
                    {
                        index = FreedPatternIndices.Dequeue();
                    }
                    else
                    {
                        index = PatternCount;
                        if (index >= Patterns.Length)
                        {
                            var newSize = Patterns.Length * 2;
                            Array.Resize(ref Patterns, newSize);
                            Array.Resize(ref PatternMethods, newSize);
                        }
                    }

                    Patterns[index] = new Regex(address);
                    PatternMethods[index] = onReceived;
                    PatternStringToIndex[address] = index;
                    PatternCount++;
                    return true;
                default: 
                    return false;
            }
        }

        public bool RemoveMethod(string address, OscActionPair onReceived)
        {
            if (string.IsNullOrEmpty(address) || onReceived == null) 
                return false;

            switch (OscParser.GetAddressType(address))
            {    
                case AddressType.Address:
                    AddressToMethod.Remove(address, onReceived);
                    return true;
                case AddressType.Pattern:
                    if (!PatternStringToIndex.TryGetValue(address, out var patternIndex))
                        return false;

                    var method = PatternMethods[patternIndex].ValueRead;
                    if (method.GetInvocationList().Length == 1)
                    {
                        Patterns[patternIndex] = null;
                        PatternMethods[patternIndex] = null;
                    }
                    else
                    {
                        PatternMethods[patternIndex] -= onReceived;
                    }

                    PatternCount--;
                    FreedPatternIndices.Enqueue(patternIndex);
                    return PatternStringToIndex.Remove(address);
                default: 
                    return false;
            }
        }

        public bool TryMatchPattern(string address, out OscActionPair method)
        {
            for (var i = 0; i < PatternCount; i++)
            {
                if (Patterns[i].IsMatch(address))
                {
                    method = PatternMethods[i];
                    return true;
                }
            }

            method = default;
            return false;
        }
        
        bool AddAddressMethod(string address, OscActionPair onReceived)
        {
            switch (OscParser.GetAddressType(address))
            {    
                case AddressType.Address:
                    AddressToMethod.Add(address, onReceived);
                    return true;
                case AddressType.Pattern:
                    int index;
                    // if a method has already been registered for this pattern, add the new delegate
                    if (PatternStringToIndex.TryGetValue(address, out index))
                    {
                        PatternMethods[index] += onReceived;
                        return true;
                    }

                    if (FreedPatternIndices.Count > 0)
                    {
                        index = FreedPatternIndices.Dequeue();
                    }
                    else
                    {
                        index = PatternCount;
                        if (index >= Patterns.Length)
                        {
                            var newSize = Patterns.Length * 2;
                            Array.Resize(ref Patterns, newSize);
                            Array.Resize(ref PatternMethods, newSize);
                        }
                    }

                    Patterns[index] = new Regex(address);
                    PatternMethods[index] = onReceived;
                    PatternStringToIndex[address] = index;
                    PatternCount++;
                    return true;
                default: 
                    return false;
            }
        }

        /// <summary>
        /// Try to match an address against all known address patterns,
        /// and add a handler for the address if a pattern is matched
        /// </summary>
        /// <param name="address">The address to match</param>
        /// <param name="allMatchedMethods"></param>
        /// <returns>True if a match was found, false otherwise</returns>
        public bool TryMatchPatternHandler(string address, List<OscActionPair> allMatchedMethods)
        {
            if (!OscParser.AddressIsValid(address))
                return false;
            
            allMatchedMethods.Clear();

            bool any = false;
            for (var i = 0; i < PatternCount; i++)
            {
                if (Patterns[i].IsMatch(address))
                {
                    var handler = PatternMethods[i];
                    AddressToMethod.Add(address, handler);
                    any = true;
                }
            }

            return any;
        }

        public bool TryMatchIncomingPattern(Regex pattern, List<OscActionPair> matchedMethods)
        {
            matchedMethods.Clear();
            foreach (var kvp in AddressToMethod.SourceToBlob)
            {
                if (!pattern.IsMatch(kvp.Key))
                    continue;
                
                if(AddressToMethod.HandleToValue.TryGetValue(kvp.Value.Handle, out var actionPair))
                    matchedMethods.Add(actionPair);
            }
            
            return false;
        }
    }
}

