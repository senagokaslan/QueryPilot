export const chartTheme = {
  primary: "var(--chart-1)",
  success: "var(--chart-2)",
  warning: "var(--chart-3)",
  danger: "var(--chart-4)",
  violet: "var(--chart-5)",
  grid: "var(--chart-grid)",
  axis: "var(--text-secondary)",
  surface: "var(--surface)",
} as const;

export const chartColors = [chartTheme.primary, chartTheme.success, chartTheme.warning, chartTheme.danger, chartTheme.violet];
