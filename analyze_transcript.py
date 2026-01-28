import re
from datetime import timedelta

def parse_time(time_str):
    parts = time_str.split(':')
    if len(parts) == 2:
        return timedelta(minutes=int(parts[0]), seconds=int(parts[1]))
    elif len(parts) == 3:
        return timedelta(hours=int(parts[0]), minutes=int(parts[1]), seconds=int(parts[2]))
    return timedelta(0)

def analyze(filename):
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()

    # Regex to find timestamps and speakers
    # Matches [mm:ss] [Talare X]
    matches = list(re.finditer(r'\[(\d{1,2}:\d{2})\] \[Talare (\d+)\]', content))
    
    speaker_durations = {}
    
    for i in range(len(matches)):
        start_time = parse_time(matches[i].group(1))
        speaker_id = int(matches[i].group(2))
        
        if i < len(matches) - 1:
            end_time = parse_time(matches[i+1].group(1))
        else:
            # Estimate last segment as 5 seconds or until next timestamp if we had the full file
            # Since we don't have the end of the file, let's assume 5 seconds for the last one
            end_time = start_time + timedelta(seconds=5)
            
        duration = (end_time - start_time).total_seconds()
        
        if speaker_id not in speaker_durations:
            speaker_durations[speaker_id] = 0
        speaker_durations[speaker_id] += duration

    print(f"{'Speaker':<10} | {'Total Duration (s)':<20} | {'Keep (>15s)?'}")
    print("-" * 50)
    
    kept_speakers = []
    dropped_speakers = []
    
    sorted_speakers = sorted(speaker_durations.items(), key=lambda x: x[1], reverse=True)
    
    for spk, dur in sorted_speakers:
        keep = "YES" if dur >= 15 else "NO"
        print(f"Talare {spk:<3} | {dur:<20.1f} | {keep}")
        if dur >= 15:
            kept_speakers.append(spk)
        else:
            dropped_speakers.append(spk)
            
    print("-" * 50)
    print(f"Total Speakers: {len(speaker_durations)}")
    print(f"Speakers kept (>15s): {len(kept_speakers)} ({kept_speakers})")
    print(f"Speakers dropped (<15s): {len(dropped_speakers)} ({dropped_speakers})")

if __name__ == "__main__":
    analyze("d:/Appar/wspr-pc/temp_transcript.txt")
