import type { SparklineSpec } from "./types";

/** Default when a column's sparkline is switched on: a plain blue line with its area tinted. */
export const DEFAULT_SPARKLINE: SparklineSpec = { type: "line", color: "#2563eb", showArea: true };
