import { GridToolbarContainer, GridToolbarColumnsButton, GridToolbarDensitySelector, GridToolbarExport, GridToolbarQuickFilter } from '@mui/x-data-grid'

export function StandardGridToolbar() {
  return (
    <GridToolbarContainer>
  <GridToolbarQuickFilter debounceMs={300} />
      <GridToolbarColumnsButton />
      <GridToolbarDensitySelector />
      <GridToolbarExport csvOptions={{ fileName: 'cine-review-export' }} />
    </GridToolbarContainer>
  )
}
