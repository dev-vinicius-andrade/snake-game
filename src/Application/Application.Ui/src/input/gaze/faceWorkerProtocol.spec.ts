import { describe, expect, it } from "vitest";
import { fromLean, toLean } from "./faceWorkerProtocol";

const categories = [{ categoryName: "jawOpen", score: 0.4 }, { categoryName: "eyeBlinkLeft", score: 0.1, index: 9, displayName: "" }];

describe("toLean", () => {
  it("keeps only what the app reads: that a face was found, its blendshape scores and its head matrix", () => {
    const lean = toLean({
      faceLandmarks: [new Array(478).fill({ x: 0, y: 0, z: 0 })],
      faceBlendshapes: [{ categories, headIndex: 0, headName: "" }],
      facialTransformationMatrixes: [{ rows: 4, columns: 4, data: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] }],
    } as never);

    expect(lean).toEqual({
      face: true,
      categories: [{ categoryName: "jawOpen", score: 0.4 }, { categoryName: "eyeBlinkLeft", score: 0.1 }],
      matrix: { data: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] },
    });
  });

  it("says no face when there are no landmarks", () => {
    expect(toLean({ faceLandmarks: [], faceBlendshapes: [], facialTransformationMatrixes: [] })).toEqual({ face: false, categories: null, matrix: null });
  });

  it("copes with a result that has nothing in it", () => {
    expect(toLean({})).toEqual({ face: false, categories: null, matrix: null });
  });

  it("sends the matrix as plain numbers so it survives the trip between threads", () => {
    const data = new Float32Array([1, 2, 3]);

    const lean = toLean({ faceLandmarks: [[]], facialTransformationMatrixes: [{ data }] });

    expect(Array.isArray(lean.matrix?.data)).toBe(true);
    expect(lean.matrix?.data).toEqual([1, 2, 3]);
  });
});

describe("fromLean", () => {
  it("builds the result the camera source reads, so it cannot tell a worker from the main thread", () => {
    const result = fromLean({ face: true, categories, matrix: { data: [1, 2, 3] } });

    expect(result.faceLandmarks).toHaveLength(1);
    expect(result.faceBlendshapes).toEqual([{ categories }]);
    expect(result.facialTransformationMatrixes).toEqual([{ data: [1, 2, 3] }]);
  });

  it("builds an empty result for no face", () => {
    const result = fromLean({ face: false, categories: null, matrix: null });

    expect(result.faceLandmarks).toEqual([]);
    expect(result.faceBlendshapes).toEqual([]);
    expect(result.facialTransformationMatrixes).toEqual([]);
  });
});
