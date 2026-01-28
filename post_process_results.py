import sys
import os

def time_to_sec(t_str):
    parts = t_str.split(':')
    h, m, s = parts
    return int(h)*3600 + int(m)*60 + float(s)

def sec_to_time(sec):
    h = int(sec // 3600)
    m = int((sec % 3600) // 60)
    s = sec % 60
    return f"{h:02d}:{m:02d}:{s:06.3f}"

def post_process(input_path, output_path, target_count=3):
    if not os.path.exists(input_path):
        print(f"Error: {input_path} not found")
        return

    segments = []
    with open(input_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
        for line in lines:
            if '|' not in line or 'Start' in line: continue
            parts = line.strip().split('|')
            segments.append({
                'start': time_to_sec(parts[0]),
                'end': time_to_sec(parts[1]),
                'speaker': int(parts[2])
            })

    # 1. Total duration per speaker
    durations = {}
    for s in segments:
        durations[s['speaker']] = durations.get(s['speaker'], 0) + (s['end'] - s['start'])

    # 2. Identify Top N dominant speakers
    sorted_spks = sorted(durations.items(), key=lambda x: x[1], reverse=True)
    dominants = {spk for spk, dur in sorted_spks[:target_count]}
    
    print(f"Top {target_count} speakers: {dominants}")

    # 3. Map ghosts to nearest dominant
    processed = []
    for i, seg in enumerate(segments):
        if seg['speaker'] in dominants:
            processed.append(seg.copy())
        else:
            # Ghost. Find nearest
            prev_dom = None
            dist_prev = float('inf')
            for j in range(i-1, -1, -1):
                if segments[j]['speaker'] in dominants:
                    prev_dom = segments[j]['speaker']
                    dist_prev = seg['start'] - segments[j]['end']
                    break
            
            next_dom = None
            dist_next = float('inf')
            for j in range(i+1, len(segments)):
                if segments[j]['speaker'] in dominants:
                    next_dom = segments[j]['speaker']
                    dist_next = segments[j]['start'] - seg['end']
                    break
            
            new_id = seg['speaker']
            if prev_dom is not None and next_dom is not None:
                new_id = prev_dom if dist_prev <= dist_next else next_dom
            elif prev_dom is not None:
                new_id = prev_dom
            elif next_dom is not None:
                new_id = next_dom
            
            processed.append({**seg, 'speaker': new_id})

    # 4. Final Merge pass (contiguous same ID)
    final = []
    for seg in processed:
        if final and final[-1]['speaker'] == seg['speaker']:
            # ALWAYS merge contiguous if it's the same person
            # (Production uses 1.0s gap, but for a clean transcript 
            # we usually want to close small holes made by ghosts)
            if (seg['start'] - final[-1]['end']) < 1.5: 
                final[-1]['end'] = seg['end']
            else:
                final.append(seg)
        else:
            final.append(seg)

    # 5. Canonicalize IDs (1, 2, 3...) based on finish order of first segment
    id_map = {}
    next_id = 1
    # Optimization: Map them in order of their FIRST appearance
    for seg in final:
        if seg['speaker'] not in id_map:
            id_map[seg['speaker']] = next_id
            next_id += 1
        seg['speaker'] = id_map[seg['speaker']]

    # 6. Save
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("Start|End|Speaker\n")
        for seg in final:
            f.write(f"{sec_to_time(seg['start'])}|{sec_to_time(seg['end'])}|{seg['speaker']}\n")
    
    print(f"Post-processed {len(segments)} -> {len(final)} segments.")
    print(f"Saved to {output_path}")
    
    print(f"Post-processed {len(segments)} -> {len(final)} segments.")
    print(f"Saved to {output_path}")

if __name__ == "__main__":
    base_dir = r"d:\Appar\wspr-pc\scripts\FullTranscriptionTest"
    post_process(os.path.join(base_dir, "Diarization_Test_3_HighSens.txt"), 
                 os.path.join(base_dir, "Final_Mapped_Result.txt"))
