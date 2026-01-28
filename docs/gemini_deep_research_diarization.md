Advanced Diarization Architectures for Long-Form Swedish Audio: Overcoming Speaker Imbalance and Gender Ambiguity in Offline Environments
1. Architectural Deconstruction of Diarization Failures in Imbalanced Regimes
The pursuit of high-fidelity speaker diarization within offline, resource-constrained environments—specifically for the TapScribe PC application targeting Swedish transcription—requires a rigorous re-evaluation of traditional clustering paradigms. The specific failure modes identified, namely the inability to cleanly separate three speakers (two males, one female) in recordings characterized by significant duration and uneven speaker distribution, are not merely implementation bugs but fundamental algorithmic deficiencies inherent to standard Agglomerative Hierarchical Clustering (AHC) and heuristic-based merging strategies. This report provides an exhaustive technical analysis of these failure mechanisms and proposes a mathematically grounded migration toward Spectral Clustering with p-Neighborhood Retention (SC-pNA) and Variational Bayesian Hidden Markov Models (VBx), integrated within the Sherpa-ONNX ecosystem.
1.1 The Density Variance Problem in Agglomerative Clustering
To address the limitations of the current "Smart Merge" logic, one must first deconstruct the behavior of AHC when applied to imbalanced data. AHC, along with K-Means, typically operates on the assumption that clusters in the embedding space (the vector space populated by x-vectors or embeddings from models like CAM++) are convex, isotropic, and, crucially, of relatively uniform density. In the specific scenario of a 90-minute recording where one speaker dominates the conversation (e.g., an interviewer or lecturer) while others interject briefly, the data geometry becomes severely skewed. The dominant speaker generates a high volume of embedding vectors, creating a dense, high-gravity cluster core. In contrast, the minority speakers, contributing fewer and shorter segments, generate sparse, dispersed clusters with higher variance due to the limited acoustic sampling available for their voice profiles.
The failure mechanism here is mathematical rather than purely acoustic. AHC algorithms utilize linkage criteria—typically Ward’s method, Average Linkage, or Complete Linkage—to decide which clusters to merge at each step of the dendrogram construction.1 Ward’s method, for instance, seeks to minimize the total within-cluster variance. In an uneven conversation, merging a sparse "minority speaker" point into a dense "majority speaker" cluster often results in a lower increase in global variance than merging two sparse, distant minority points together. Consequently, the "Smart Merge" logic, likely driven by variance minimization or simple cosine distance thresholds, collapses the minority male speaker into the majority male speaker. This results in the observed cross-gender or cross-speaker merging, effectively "absorbing" the less frequent speaker into the dominant identity.
Furthermore, heuristic thresholding (e.g., "merge if similarity > 0.7") is mathematically flawed for long-form audio because the optimal threshold is non-stationary. Background noise levels, speaker fatigue, microphone positioning, and emotional affect shift over the course of an hour, causing the baseline similarity scores to drift.3 A fixed threshold that successfully separates speakers in the first ten minutes may cause severe over-segmentation or under-clustering by the fiftieth minute, as the "Smart Merge" logic lacks the global context to normalize these local variations.
1.2 The Non-Convexity of Long-Form Manifolds
Long-form recordings introduce the phenomenon of acoustic drift, which fundamentally alters the shape of speaker clusters. Over the course of a long session, a single speaker’s voice does not form a tight, spherical distribution in the embedding space. Instead, it forms a complex, elongated manifold—a "snake-like" structure where the head (minute 1) may be far from the tail (minute 60) in Euclidean terms, yet connected by a continuous chain of intermediate segments.
Standard convex clustering algorithms struggle profoundly with these geometries.5 K-Means, which partitions space using Voronoi tessellations based on centroids, will inevitably fracture a long, curved manifold into multiple spherical sub-clusters, leading to the "over-segmentation" reported. AHC is susceptible to the "chaining effect," where distinct manifolds (e.g., Male 1 and Male 2) are merged because they touch at a single point of acoustic similarity—perhaps a moment of laughter or a shared backchannel utterance like "mm"—bridging the two distinct clusters into one.
This issue is exacerbated by the "2 Male / 1 Female" demographic split. While the female speaker’s pitch (fundamental frequency $F_0$) typically places her embeddings in a distinct region of the vector space, the two male speakers often occupy adjacent or overlapping manifolds. If their timber and prosody are similar, the "gap" between their clusters becomes extremely narrow. Without explicit topological constraints or "cannot-link" instructions derived from temporal overlap or gender priors, unsupervised clustering algorithms will treat these proximate manifolds as a single entity, failing to distinguish between the two male identities.7
1.3 Limitations of Current Heuristics and the Need for Spectral Approaches
The reliance on custom "Smart Merge" logic represents a heuristic attempt to patch these algorithmic deficiencies. However, heuristics are brittle. They require constant tuning of parameters—such as min_speech_duration or similarity thresholds—that rarely generalize across different recording conditions.3 The research indicates that to robustly separate three speakers with uneven distribution, the system must move beyond distance-based heuristics to graph-theoretic approaches. Spectral clustering, specifically variants designed to handle imbalanced data like SC-pNA, utilizes the eigenvalues (spectrum) of the data's Laplacian matrix to map these complex, non-convex manifolds into a lower-dimensional space where they become linearly separable.1 This transformation allows the system to identify the global structure of the data—recognizing that the "sparse" minority speaker is a distinct entity from the "dense" majority speaker based on connectivity rather than just density.
2. Advanced Acoustic Modeling: Embeddings for Swedish Prosody
The performance of any clustering backend is strictly bounded by the discriminative power of the input embeddings. If the embedding model cannot mathematically distinguish between the acoustic characteristics of Male 1 and Male 2, no amount of downstream clustering optimization will recover the true speaker labels. For TapScribe PC, the requirement for offline Swedish transcription necessitates a departure from generic, English-centric models toward architectures that are sensitive to the specific phonological and prosodic features of the Swedish language.
2.1 The Case for Language-Specific Embeddings: KB-Whisper
Standard speaker embedding models, such as those trained on the VoxCeleb dataset (typically ResNet34 or ECAPA-TDNN architectures), are trained primarily on English celebrity interviews. While these models capture general vocal characteristics, they may under-weight prosodic features critical to Swedish. Swedish is a pitch-accent language, distinguishing words not just by phonemes but by tonal contours (Accent I and Accent II, e.g., the difference between "anden" the duck and "anden" the spirit). Generic models often treat these prosodic variations as intra-speaker variability (noise) rather than inter-speaker identity cues.
The National Library of Sweden (KBLab) has released kb-whisper, a suite of Whisper models fine-tuned on a massive corpus of 50,000 hours of transcribed Swedish speech.10 These models demonstrate a significant performance advantage, reporting an average 47% reduction in Word Error Rate (WER) compared to OpenAI's generic whisper-large-v3 on Swedish benchmarks.
More importantly for diarization, recent research into "Whisper Speaker Identification" (WSI) frameworks demonstrates that the encoder portion of the Whisper architecture contains rich, high-fidelity speaker identity information.14 The encoder, which processes the log-Mel spectrogram into a sequence of latent representations, captures detailed acoustic and prosodic features in its intermediate layers (specifically blocks 12 through 24 in large models) before the decoder forces the representation into textual tokens. By repurposing the kb-whisper encoder as a feature extractor, TapScribe PC can generate embeddings that are inherently attuned to Swedish vocal patterns.16
Sherpa-ONNX facilitates this by supporting the export of Whisper components as standalone ONNX modules. The operational strategy involves extracting the kb-whisper-small or kb-whisper-medium encoder. The output of this encoder—a tensor of shape [batch, frames, hidden_dim]—must then be pooled to form a fixed-length d-vector. Integrating this encoder provides a dual benefit: it utilizes a model already optimized for the specific acoustic environment of Swedish data, and it allows for memory sharing if the same model is used for the subsequent ASR transcription step.
2.2 Dense TDNN Architectures: WeSpeaker CAM++
If the computational overhead of running a Transformer-based Whisper encoder for diarization proves prohibitive for the target offline PC hardware, the CAM++ (Context-Aware Masking) architecture offered by WeSpeaker represents the current state-of-the-art in lightweight speaker verification.17
CAM++ utilizes a Densely Connected Time-Delay Neural Network (D-TDNN) backbone. Unlike standard ResNet architectures, which process audio frames with fixed receptive fields, TDNNs are designed to capture long-range temporal dependencies. CAM++ enhances this with a multi-granularity pooling mechanism that aggregates features at different temporal scales. This architecture is specifically robust to the "uneven speaking time" problem because it can stabilize embeddings even for relatively short segments (e.g., the brief interjections of the minority speakers) by leveraging wider context windows without exponentially increasing parameter count.15
Sherpa-ONNX provides native support for wespeaker-voxceleb-cam++ models, including quantized int8 versions that run efficiently on consumer CPUs via the ONNX Runtime.19 While these models are text-independent and language-agnostic (trained on VoxCeleb), their superior handling of variable-duration segments makes them a strong candidate for resolving the "sparse cluster" issue where minority speakers are often misclassified due to insufficient data.
2.3 Feature Robustness and Comparative Strategy
For the specific challenge of separating two male speakers, the embedding space must maximize inter-class variance. A critical insight from the research is that generic models often conflate speakers of the same gender and demographic because they rely heavily on fundamental frequency ($F_0$) and gross spectral shape. Swedish-specific models like kb-whisper, by virtue of being trained on Swedish prosody, are hypothesized to leverage pitch accent and vowel formant transitions—features that differ even between speakers of the same gender—to provide better separation.10
Therefore, the recommended strategy involves an A/B evaluation within the TapScribe development cycle. The primary pipeline should implement the wespeaker-cam++ model for its speed and robustness to duration variability. However, if cross-gender or same-gender confusion persists, the system should fallback to (or optionally enable) the kb-whisper encoder extraction path. This ensures that difficult cases utilize the most discriminative feature set available, trading off some computational latency for higher diarization accuracy.
3. Algorithmic Foundations: Spectral Clustering and P-Neighborhoods
Moving beyond the limitations of AHC requires adopting a clustering framework that explicitly addresses the non-convexity and density variance of the data. Spectral Clustering serves as this foundation, treating the clustering problem not as a geometric partitioning of space, but as a graph partitioning problem.
3.1 Mathematical Formulation of Spectral Clustering
In spectral clustering, the dataset is represented as a weighted undirected graph $G = (V, E)$, where each vertex $v_i$ represents a speech segment (embedding) and the weight $w_{ij}$ of the edge connecting $v_i$ and $v_j$ represents their similarity (typically Gaussian kernel or cosine similarity).5 The objective is to find a partition of the graph such that the edges between different groups have very low weights (different speakers) and the edges within a group have high weights (same speaker).
The core of the algorithm involves the Graph Laplacian, a matrix that captures the connectivity structure of the data.
The unnormalized Laplacian is defined as $L = D - W$, where $W$ is the adjacency matrix (similarity matrix) and $D$ is the degree matrix (a diagonal matrix where $D_{ii} = \sum_j W_{ij}$).21
However, for diarization, the Normalized Laplacian is preferred to handle variations in cluster density. The symmetric normalized Laplacian is defined as:


$$L_{sym} = I - D^{-1/2} W D^{-1/2}$$

The eigenvectors of this matrix (specifically the eigenvectors corresponding to the smallest non-zero eigenvalues) provide a spectral embedding of the data. In this new low-dimensional space, the complex, "snake-like" manifolds of the speakers are unfolded into orthogonal clusters that can be trivially separated by K-Means.6
3.2 Solving the Imbalance: Spectral Clustering with p-Neighborhood Affinity (SC-pNA)
Standard spectral clustering constructs the adjacency matrix $W$ using either a fixed $\epsilon$-neighborhood (connecting all points within distance $\epsilon$) or a k-nearest neighbor graph. Both approaches fail in the imbalanced scenario described for TapScribe. If $\epsilon$ is chosen to capture the dense "majority speaker" cluster, it will likely disconnect the sparse "minority speaker" cluster, treating its points as noise or outliers. Conversely, if $\epsilon$ is relaxed to connect the sparse cluster, it will create "bridges" between the majority and minority clusters, leading to merges.1
SC-pNA (Spectral Clustering on p-Neighborhood Retained Affinity Matrix) is a novel modification specifically designed to address this.1 Instead of a global threshold, SC-pNA constructs the affinity matrix adaptively. For each node (speech segment), it retains only the top $p\%$ of similarity scores, effectively pruning weak connections that might bridge distinct clusters.
The algorithm proceeds as follows:
Affinity Construction: Compute the full cosine similarity matrix $S$.
Adaptive Pruning: For each row $i$ in $S$:
Identify the two distinct clusters of similarity values (high vs. low) using 2-means clustering on the row's values.
Retain only the values in the "high similarity" cluster that are within the top $p\%$.9
Set all other values to 0. This creates a sparse, locally adapted adjacency matrix $A$.
Symmetrization: Ensure the matrix is symmetric: $W = \frac{1}{2}(A + A^T)$.
Eigengap Estimation: Compute the eigenvalues of the Laplacian $L_{sym}$. The number of clusters $k$ is estimated by finding the maximum eigengap (the difference $\lambda_{k+1} - \lambda_k$). This automatic estimation is crucial for the user's issue of reliably detecting exactly 3 speakers without hard-coding the value.1
By normalizing connectivity based on the local neighborhood of each point rather than a global metric, SC-pNA ensures that the sparse minority clusters remain connected internally without being absorbed by the high-gravity majority cluster.
3.3 Implementation in C# via Math.NET Numerics
Implementing SC-pNA in the TapScribe offline C# environment requires bypassing Sherpa-ONNX's basic clustering and utilizing a dedicated numerical library. Math.NET Numerics is the standard for such operations in the.NET ecosystem.24
The implementation pipeline involves:
Data Ingestion: Receive float embeddings from Sherpa-ONNX.
Matrix Operations: Use MathNet.Numerics.LinearAlgebra.Double.DenseMatrix to hold the affinity matrix.
Laplacian Computation: Calculate $D^{-1/2}$ efficiently (inverse square root of row sums). Compute $L_{sym}$ via matrix multiplication.
Eigendecomposition: Use the .Evd() method (Eigenvalue Decomposition) on the symmetric Laplacian. Since $L_{sym}$ is symmetric positive semi-definite, this operation is numerically stable.25
Projection: Extract the eigenvectors corresponding to the $k$ smallest eigenvalues (excluding the first eigenvalue which is always 0 for connected graphs).
Clustering: Perform K-Means on the rows of the eigenvector matrix to assign final speaker labels.
This approach replaces the heuristic "Smart Merge" with a rigorous linear algebra operation that naturally handles the complex geometries of long-form speech data.
4. Constrained Optimization Strategies for Gender Separation
While SC-pNA addresses the structural imbalances, the specific "2 Male / 1 Female" confusion requires a mechanism to inject domain knowledge—specifically gender constraints—into the clustering process. Constrained Spectral Clustering allows the imposition of "Must-Link" and "Cannot-Link" constraints to resolve ambiguities that unsupervised methods cannot.26
4.1 Constraint Types and Generation
In the context of diarization, two primary sources of constraints can be leveraged:
Temporal Constraints (Overlap): If the segmentation model (e.g., Pyannote) detects that two segments occur simultaneously (overlapping speech), they cannot belong to the same speaker. This generates a Cannot-Link (CL) constraint between the embeddings of these two segments.28
Gender Constraints: A lightweight, pre-trained gender classifier (or a GMM trained on generic male/female data) can tag each segment with a probability $P(Male)$ or $P(Female)$.
If Segment A has $P(M) > 0.9$ and Segment B has $P(F) > 0.9$, a Cannot-Link constraint is generated.
Crucially, this helps separate the two males. Even if Male 1 and Male 2 are acoustically similar, the algorithm is forced to separate them from the Female cluster. This constraint pressure alters the spectral embedding, often pushing the two male clusters apart in the remaining dimensions as the algorithm seeks the optimal cut that satisfies the gender separation.30
4.2 Mathematical Implementation of Constraints
Integrating constraints into spectral clustering can be achieved by modifying the affinity matrix $W$ before the Laplacian calculation. This is often referred to as "modifying the graph topology".27
For a set of Cannot-Link constraints $C_{CL} = \{(i,j)\}$:


$$W_{ij} = W_{ji} = 0 \quad \forall (i,j) \in C_{CL}$$

For Must-Link constraints (e.g., highly similar adjacent segments):


$$W_{ij} = W_{ji} = 1 \quad \text{(or maximum affinity)}$$
More advanced methods formulate this as a Generalized Eigenvalue Problem:


$$L v = \lambda Q v$$

where $Q$ is a constraint matrix encoding the penalties for violating constraints.30 However, for the specific problem of preventing overlap merges and gender confusion, the direct modification of the adjacency matrix (setting weights to 0) is computationally efficient and sufficiently effective for offline applications. By zeroing out the affinity between overlapping segments, we ensure they are orthogonal in the spectral domain, forcing the K-Means step to assign them to different clusters.33
5. Probabilistic Refinement via VBx
For the highest possible accuracy, particularly to correct "over-segmentation" (where a single speaker is split into multiple clusters), the industry standard has shifted toward VBx (Variational Bayesian HMM Clustering of x-vectors).35 VBx effectively acts as a global re-segmentation and refinement stage that follows the initial spectral clustering.
5.1 The Bayesian HMM Framework
Unlike AHC or Spectral Clustering, which treat segments as independent points in space (bag-of-vectors assumption), VBx models the sequence of speakers using a Hidden Markov Model (HMM).
States: Each state in the HMM represents a speaker (derived from the initial clustering).
Transitions: The transition probabilities model the likelihood of speaker turns. Crucially, the "loop probability" (staying in the same state) is high, which imposes a "stickiness" prior. This explicitly combats over-segmentation by penalizing rapid, unmotivated switching between speaker labels.28
Emissions: The emission probability for each state is modeled using Probabilistic Linear Discriminant Analysis (PLDA). PLDA provides a rigorous statistical framework for determining whether two embeddings belong to the same underlying identity, factoring in within-speaker variability (channel noise, emotion) versus between-speaker variability.35
5.2 VBx for Re-segmentation
In the proposed TapScribe pipeline, VBx serves as the "post-processing" engine. After SC-pNA provides the initial cluster centroids (answering "how many speakers?" and "roughly where are they?"), VBx takes these centroids as initialization and runs the Variational Bayes inference algorithm.
This process iteratively updates the assignment of segments to speakers to maximize the Evidence Lower Bound (ELBO) of the data likelihood.35 Because VBx looks at the global sequence, it can "repair" errors where the initial clustering might have split Male 1 into two clusters due to a temporary voice change. The HMM structure "knows" that it is unlikely for a new speaker to appear for just 2 seconds and then disappear, and will likely re-merge these fragments into the main Male 1 cluster.
Implementing VBx in C# involves porting the forward-backward algorithm and PLDA scoring. While complex, the mathematical operations (matrix multiplication, log-sum-exp) are supported by Math.NET Numerics. The computational cost is linear with the duration of the audio, making it feasible for offline processing of 1-hour files.35
6. Operational Implementation in.NET/Sherpa-ONNX
The theoretical architecture must be realized within the constraints of the.NET ecosystem and the Sherpa-ONNX library. This section details the operational integration.
6.1 Sherpa-ONNX Configuration and C# Bindings
Sherpa-ONNX exposes its functionality via the OfflineSpeakerDiarization class in C#.38 The configuration struct OfflineSpeakerDiarizationConfig is the entry point for tuning.
Segmentation Model: The configuration must point to sherpa-onnx-pyannote-segmentation-3.0. This model is superior to energy-based VADs as it explicitly detects overlapping speech regions, providing the necessary [start, end, overlap_status] metadata for the constraint generation step.40
Embedding Model: This should be configured to load the wespeaker-voxceleb-cam++ ONNX model (or the exported kb-whisper encoder).
Clustering Parameters: Crucially, the internal clustering of Sherpa-ONNX (often a basic AHC implementation) should be bypassed or set to generate a high number of clusters (over-segmentation) to be refined by the custom C# pipeline. The API allows retrieving the raw embeddings, which is the requisite input for the custom SC-pNA and VBx implementations.38
6.2 Memory Management for Long Audio
Processing 60+ minute audio files (often >600MB uncompressed WAV) requires careful memory management to avoid OutOfMemoryExceptions in the.NET runtime.
Circular Buffer: Sherpa-ONNX provides a native CircularBuffer class accessible via C#.38 The application should not load the entire file into a byte array. Instead, it should stream the audio from disk, feeding chunks (e.g., 100ms) into the CircularBuffer via the AcceptWaveform method.
Incremental Processing: The diarization engine can process the audio in sliding windows (e.g., 5-minute buffers) to extract embeddings. These light-weight embedding vectors (float) can be stored in a List<float> in managed memory, which consumes negligible RAM compared to the raw audio. The computationally expensive clustering (SC-pNA/VBx) is then performed on this collection of vectors once the entire file (or a sufficient block) has been processed.37
6.3 Post-Processing and Gender Logic
To address the specific user request regarding 2 Male / 1 Female separation, a post-processing stage is necessary.
Pitch Detection (LibPyin): Since gender separation is strongly correlated with fundamental frequency ($F_0$), integrating a pitch detection library like LibPyin (a C/C++ library with C# bindings via P/Invoke) allows for calculating the median pitch of each cluster.42
Logic:
Compute median $F_0$ for each of the 3 final clusters.
If Cluster A ($F_0 \approx 120$Hz) and Cluster B ($F_0 \approx 125$Hz) are close, and Cluster C ($F_0 \approx 210$Hz) is distant, label C as Female.
If Cluster A and B were merged by the algorithm but show a bimodal pitch distribution (two distinct peaks in the pitch histogram), this signals an under-clustering error. The system can then trigger a re-clustering of just that specific cluster with $k=2$ to forcibly separate the two males.44
7. Conclusion
The "Smart Merge" failure experienced in TapScribe PC is a symptom of applying density-biased algorithms (AHC) to density-skewed data (imbalanced speakers). The solution lies in a fundamental architectural shift:
Replace AHC with Spectral Clustering (SC-pNA): This handles the imbalance by normalizing connectivity locally, preventing the dominant speaker from absorbing the minority speakers.
Enforce Constraints: Use "Cannot-Link" constraints derived from pyannote/segmentation overlap detection and gender/pitch analysis to mathematically prohibit invalid merges.
Enhance Embeddings: Move to kb-whisper or wespeaker-cam++ to leverage Swedish-specific prosodic features and robust dense-TDNN architectures.
Refine with VBx: Implement a Bayesian HMM layer to smooth transitions and correct segmentation errors using global probability maximization.
By implementing this hybrid pipeline within the high-performance Sherpa-ONNX/C# environment, TapScribe PC can achieve robust, offline diarization that accurately disentangles the complex "2 Male / 1 Female" scenario in long-form Swedish audio.
Table 1: Comparison of Clustering Algorithms for Diarization
Feature
Agglomerative HC (Current)
Spectral Clustering (Standard)
SC-pNA (Recommended)
VBx (State-of-the-Art)
Core Principle
Greedy variance minimization
Graph cut / Eigenvalues
Adaptive Graph cut
Bayesian HMM / PLDA
Handling Imbalance
Poor (Absorbs small clusters)
Moderate
Excellent (Local normalization)
Excellent (Temporal priors)
Handling Non-Convexity
Poor (Chaining effect)
Excellent (Manifold unfolding)
Excellent
Good (Probabilistic)
Parameter Sensitivity
High (Distance threshold)
High (Sigma scaling)
Low (Auto-tuned via Eigengap)
Moderate (PLDA priors)
Computational Cost
$O(N^2)$ to $O(N^3)$
$O(N^3)$ (Eigendecomposition)
$O(N^3)$
$O(N)$ (Linear with time)
Implementation
Native in Sherpa
Requires Math.NET
Requires Math.NET
Requires Custom C# Port

Table 2: Recommended Embedding Models for Swedish Offline Diarization
Model Family
ResNet34 (Baseline)
WeSpeaker CAM++
KB-Whisper Encoder
Architecture
CNN
Dense TDNN
Transformer
Training Data
VoxCeleb (English/Multi)
VoxCeleb (English/Multi)
50k Hours Swedish
Prosody Sensitivity
Low
Moderate
High (Native Swedish)
Short Segment Robustness
Low
High (Multi-scale pooling)
High (Attention mech)
Inference Speed (CPU)
Fast
Very Fast (Int8)
Moderate/Slow
Sherpa-ONNX Support
Native
Native
Export Required

Citerade verk
Self-Tuning Spectral Clustering for Speaker Diarization - arXiv, hämtad januari 15, 2026, https://arxiv.org/html/2410.00023v2
On the use of Agglomerative and Spectral Clustering in Speaker Diarization of Meetings - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/odyssey_2012/luque12_odyssey.pdf
Sherpa-ONNX VAD Settings - by Nadira Povey - Medium, hämtad januari 15, 2026, https://medium.com/@nadirapovey/sherpa-onnx-vad-settings-0d7a9854e018
A robust stopping criterion for agglomerative hierarchical clustering in a speaker diarization system | Request PDF - ResearchGate, hämtad januari 15, 2026, https://www.researchgate.net/publication/221479915_A_robust_stopping_criterion_for_agglomerative_hierarchical_clustering_in_a_speaker_diarization_system
SpectralClustering — scikit-learn 1.8.0 documentation, hämtad januari 15, 2026, https://scikit-learn.org/stable/modules/generated/sklearn.cluster.SpectralClustering.html
Spectral Clustering: A Comprehensive Guide for Beginners - GeeksforGeeks, hämtad januari 15, 2026, https://www.geeksforgeeks.org/machine-learning/spectral-clustering-a-comprehensive-guide-for-beginners/
Speaker Clustering For Speech Recognition using The Parameters Characterizing Vocal-tract Dimensions - Microsoft, hämtad januari 15, 2026, https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/NaitoDeng1998.pdf
Multimodal Clustering with Role Induced Constraints for Speaker Diarization - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/interspeech_2022/flemotomos22_interspeech.pdf
Self-Tuning Spectral Clustering for Speaker Diarization - ResearchGate, hämtad januari 15, 2026, https://www.researchgate.net/publication/390537014_Self-Tuning_Spectral_Clustering_for_Speaker_Diarization
Swedish Whispers; Leveraging a Massive Speech Corpus for Swedish Speech Recognition - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/interspeech_2025/vesterbacka25_interspeech.pdf
[2505.17538] Swedish Whispers; Leveraging a Massive Speech Corpus for Swedish Speech Recognition - arXiv, hämtad januari 15, 2026, https://arxiv.org/abs/2505.17538
Welcome KB-Whisper, a new fine-tuned Swedish Whisper model! - The KBLab Blog, hämtad januari 15, 2026, https://kb-labb.github.io/posts/2025-03-07-welcome-KB-Whisper/
KBLab/kb-whisper-large - Hugging Face, hämtad januari 15, 2026, https://huggingface.co/KBLab/kb-whisper-large
Whisper Speaker Identification: Leveraging Pre-Trained Multilingual Transformers for Robust Speaker Embeddings - arXiv, hämtad januari 15, 2026, https://arxiv.org/html/2503.10446v1
Partial Multi-Scale Feature Aggregation for Speaker Verification using Whisper Models - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/interspeech_2024/zhao24f_interspeech.pdf
Export Whisper to ONNX — sherpa 1.3 documentation, hämtad januari 15, 2026, https://k2-fsa.github.io/sherpa/onnx/pretrained_models/whisper/export-onnx.html
openspeech/wespeaker-models at main - Hugging Face, hämtad januari 15, 2026, https://huggingface.co/openspeech/wespeaker-models/tree/main
modelscope/3D-Speaker: A Repository for Single- and Multi-modal Speaker Verification, Speaker Recognition and Speaker Diarization - GitHub, hämtad januari 15, 2026, https://github.com/modelscope/3D-Speaker
APKs for Speaker Identification - GitHub Pages, hämtad januari 15, 2026, https://k2-fsa.github.io/sherpa/onnx/speaker-identification/apk.html
A Tutorial on Spectral Clustering - People - MIT, hämtad januari 15, 2026, https://people.csail.mit.edu/dsontag/courses/ml14/notes/Luxburg07_tutorial_spectral_clustering.pdf
Spectral clustering - Wikipedia, hämtad januari 15, 2026, https://en.wikipedia.org/wiki/Spectral_clustering
10 Spectral Clustering, hämtad januari 15, 2026, https://users.cs.utah.edu/~jeffp/teaching/DM/LN/L10-spectral.pdf
Self-Tuning Spectral Clustering for Speaker Diarization - arXiv, hämtad januari 15, 2026, https://arxiv.org/html/2410.00023v1
mathnet/mathnet-numerics: Math.NET Numerics - GitHub, hämtad januari 15, 2026, https://github.com/mathnet/mathnet-numerics
Spectral Data Clustering from Scratch Using C# -- Visual Studio Magazine, hämtad januari 15, 2026, https://visualstudiomagazine.com/articles/2023/12/18/spectral-data-clustering.aspx
An Algorithm for Clustering with Confidence-Based Must-Link and Cannot-Link Constraints, hämtad januari 15, 2026, https://hochbaum.ieor.berkeley.edu/html/pub/BH-IJOC-must-link-cannot-link-clustering2024.pdf
Constrained Clustering via Spectral Regularization, hämtad januari 15, 2026, https://www.ee.columbia.edu/~zgli/papers/CVPR09_CCSR.pdf
Multi-Stream Extension of Variational Bayesian HMM Clustering (MS-VBx) for Combined End-to-End and Vector Clustering-based Diarization - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/interspeech_2023/delcroix23_interspeech.pdf
How does speech recognition handle overlapping speech? - Milvus, hämtad januari 15, 2026, https://milvus.io/ai-quick-reference/how-does-speech-recognition-handle-overlapping-speech
Simple and Scalable Constrained Clustering: A Generalized Spectral Method - UCLA Mathematics, hämtad januari 15, 2026, https://www.math.ucla.edu/~mihai/consClust_AISTATS.pdf
Multi-way Constrained Spectral Clustering by Nonnegative Restriction - Han Hu, hämtad januari 15, 2026, https://ancientmooner.github.io/doc/ICPR12_NCSC_final.pdf
On Constrained Spectral Clustering and Its Applications - ResearchGate, hämtad januari 15, 2026, https://www.researchgate.net/publication/221659821_On_Constrained_Spectral_Clustering_and_Its_Applications
Semi-Supervised Clustering via Constraints Self-Learning - MDPI, hämtad januari 15, 2026, https://www.mdpi.com/2227-7390/13/9/1535
Affinity and Penalty Jointly Constrained Spectral Clustering With All-Compatibility, Flexibility, and Robustness - NIH, hämtad januari 15, 2026, https://pmc.ncbi.nlm.nih.gov/articles/PMC4990515/
Bayesian HMM clustering of x-vector sequences (VBx) in speaker diarization: Theory, implementation and analysis on standard tasks - Faculty of Information Technology, hämtad januari 15, 2026, https://www.fit.vut.cz/research/result/c175852/.en
VBx Clustering in Speaker Diarization - Emergent Mind, hämtad januari 15, 2026, https://www.emergentmind.com/topics/vbx-clustering
Online Speaker Diarization with Core Samples Selection - ISCA Archive, hämtad januari 15, 2026, https://www.isca-archive.org/interspeech_2022/yue22b_interspeech.pdf
sherpa_onnx package - github.com/k2-fsa/sherpa-onnx/scripts/go - Go Packages, hämtad januari 15, 2026, https://pkg.go.dev/github.com/k2-fsa/sherpa-onnx/scripts/go
C# API — sherpa 1.3 documentation, hämtad januari 15, 2026, https://k2-fsa.github.io/sherpa/onnx/csharp-api/index.html
Speaker Diarization — sherpa 1.3 documentation - GitHub Pages, hämtad januari 15, 2026, https://k2-fsa.github.io/sherpa/onnx/speaker-diarization/index.html
csukuangfj/sherpa-onnx-pyannote-segmentation-3-0 - Hugging Face, hämtad januari 15, 2026, https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0
xstreck1/LibPyin: Pitch / fundamental frequency detection library for C,C++,C - GitHub, hämtad januari 15, 2026, https://github.com/xstreck1/LibPyin
libpyin/pyin.c at master - GitHub, hämtad januari 15, 2026, https://github.com/Sleepwalking/libpyin/blob/master/pyin.c
Pitch detection from FFT in C# - Stack Overflow, hämtad januari 15, 2026, https://stackoverflow.com/questions/58914307/pitch-detection-from-fft-in-c-sharp
What is the best C# Library for detecting pitch from voice singing (through microphone)? : r/csharp - Reddit, hämtad januari 15, 2026, https://www.reddit.com/r/csharp/comments/101m5ew/what_is_the_best_c_library_for_detecting_pitch/
