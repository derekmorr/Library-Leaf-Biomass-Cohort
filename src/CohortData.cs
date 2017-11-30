namespace Landis.Library.LeafBiomassCohorts
{
    /// <summary>
    /// Data for an individual cohort that is not shared with other cohorts.
    /// </summary>
    public struct CohortData
    {
        /// <summary>
        /// The cohort's age (years).
        /// </summary>
        public ushort Age;

        //---------------------------------------------------------------------

        /// <summary>
        /// The cohort's wood biomass 
        /// </summary>
        public float WoodBiomass;

        //---------------------------------------------------------------------

        /// <summary>
        /// The cohort's wood biomass 
        /// </summary>
        public float LeafBiomass;

        //---------------------------------------------------------------------

        //---------------------------------------------------------------------
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="age">
        /// The cohort's age.
        /// </param>
        /// <param name="biomass">
        /// The cohort's biomass.
        /// </param>
        public CohortData(ushort age,
                          float woodBiomass,
                          float leafBiomass)
        {
            this.Age = age;
            this.WoodBiomass = woodBiomass;
            this.LeafBiomass = leafBiomass;
        }
    }
}
