import sys
import os

def time_to_sec(t_str):
    parts = t_str.split(':')
    h, m, s = parts
    return int(h)*3600 + int(m)*60 + float(s)

def load_segments(path):
    segments = []
    with open(path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
        for line in lines:
            if '|' not in line or 'Start' in line: continue
            parts = line.strip().split('|')
            segments.append({
                'start': time_to_sec(parts[0]),
                'end': time_to_sec(parts[1]),
                'speaker': int(parts[2])
            })
    return segments

def calculate_accuracy(facit, test):
    # Calculate total duration
    total_duration = sum((s['end'] - s['start']) for s in facit)
    
    # Try all possible speaker mappings (3! = 6 permutations for 3 speakers)
    from itertools import permutations
    
    test_speakers = set(s['speaker'] for s in test)
    facit_speakers = set(s['speaker'] for s in facit)
    
    best_accuracy = 0
    best_mapping = {}
    
    # Try all permutations
    for perm in permutations(facit_speakers):
        mapping = {test_spk: facit_spk for test_spk, facit_spk in zip(sorted(test_speakers), perm)}
        
        correct_duration = 0.0
        for tseg in test:
            mapped_spk = mapping.get(tseg['speaker'], tseg['speaker'])
            
            # Find overlapping facit segments with same speaker
            for fseg in facit:
                if fseg['speaker'] != mapped_spk:
                    continue
                
                overlap_start = max(tseg['start'], fseg['start'])
                overlap_end = min(tseg['end'], fseg['end'])
                
                if overlap_end > overlap_start:
                    correct_duration += (overlap_end - overlap_start)
        
        accuracy = (correct_duration / total_duration) * 100
        if accuracy > best_accuracy:
            best_accuracy = accuracy
            best_mapping = mapping
    
    return best_accuracy, best_mapping

if __name__ == "__main__":
    base_dir = r"d:\Appar\wspr-pc\scripts\FullTranscriptionTest"
    
    facit = load_segments(os.path.join(base_dir, "obj", "Debug", "facit.klang.lack.txt"))
    test = load_segments(os.path.join(base_dir, "Final_Mapped_Result.txt"))
    
    accuracy, mapping = calculate_accuracy(facit, test)
    
    print(f"\n{'='*60}")
    print(f"ACCURACY COMPARISON")
    print(f"{'='*60}")
    print(f"Facit segments: {len(facit)}")
    print(f"Test segments:  {len(test)}")
    print(f"\nBest speaker mapping: {mapping}")
    print(f"Accuracy: {accuracy:.2f}%")
    print(f"{'='*60}\n")
