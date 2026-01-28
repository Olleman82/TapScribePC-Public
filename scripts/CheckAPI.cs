using System;
using SherpaOnnx;

class Program {
    static void Main() {
        var config = new SpeakerEmbeddingExtractorConfig();
        Console.WriteLine("SpeakerEmbeddingExtractorConfig exists");
        var extractor = new SpeakerEmbeddingExtractor(config);
        Console.WriteLine("SpeakerEmbeddingExtractor exists");
    }
}
