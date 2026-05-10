import { mergeSections } from '../utils.js';
import { createJobsActionsSection } from './jobs.actions.js';
import { createJobsDrawerSection } from './jobs.drawer.js';
import { createJobsMetricsSection } from './jobs.metrics.js';

export function createJobsSection() {
  return mergeSections(
    createJobsMetricsSection(),
    createJobsActionsSection(),
    createJobsDrawerSection(),
  );
}
