using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Landis.Core;
using Landis.SpatialModeling;
using log4net;
using System;

namespace Landis.Library.LeafBiomassCohorts
{
    /// <summary>
    /// The cohorts for a particular species at a site.
    /// </summary>
    public class SpeciesCohorts
        : ISpeciesCohorts,
          AgeOnlyCohorts.ISpeciesCohorts,
          BiomassCohorts.ISpeciesCohorts
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly bool isDebugEnabled = log.IsDebugEnabled;

        //---------------------------------------------------------------------

        private ISpecies species;
        private bool isMaturePresent;

        //  Cohort data is in oldest to youngest order.
        private List<CohortData> cohortData;

        //---------------------------------------------------------------------

        public int Count
        {
            get {
                return cohortData.Count;
            }
        }
        //---------------------------------------------------------------------
        public Cohort Get(int index)
        {
            try
            {
                Cohort cohort = new Cohort(species, cohortData[index]);
                return cohort;
            }
            catch
            {
                return null;
            }
        }
        //---------------------------------------------------------------------
        public void RemoveCohort(Cohort cohort,
                                  ActiveSite site,
                                  ExtensionType disturbanceType)
        {
            CohortData thisCohortData = cohort.Data;
            int index = cohortData.IndexOf(thisCohortData);
            RemoveCohort(index, cohort, site, disturbanceType);
        }


        //---------------------------------------------------------------------

        public ISpecies Species
        {
            get {
                return species;
            }
        }

        //---------------------------------------------------------------------

        public bool IsMaturePresent
        {
            get {
                return isMaturePresent;
            }
        }

        //---------------------------------------------------------------------

        public ICohort this[int index]
        {
            get {
                return new Cohort(species, cohortData[index]);
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// An iterator from the oldest cohort to the youngest.
        /// </summary>
        public OldToYoungIterator OldToYoung
        {
            get {
                return new OldToYoungIterator(this);
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance with one young cohort (age = 1).
        /// </summary>
        public SpeciesCohorts(ISpecies species,
                                ushort initialAge,
                              float initialWoodBiomass,
                              float initialLeafBiomass)
        {
            this.species = species;
            this.cohortData = new List<CohortData>();
            this.isMaturePresent = false;
            AddNewCohort(initialAge, initialWoodBiomass, initialLeafBiomass);
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Creates a copy of a species' cohorts.
        /// </summary>
        public SpeciesCohorts Clone()
        {
            SpeciesCohorts clone = new SpeciesCohorts(this.species);
            clone.cohortData = new List<CohortData>(this.cohortData);
            clone.isMaturePresent = this.isMaturePresent;
            return clone;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance with no cohorts.
        /// </summary>
        /// <remarks>
        /// Private constructor used by Clone method.
        /// </remarks>
        private SpeciesCohorts(ISpecies species)
        {
            this.species = species;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Adds a new cohort.
        /// </summary>
        public void AddNewCohort(ushort age, float initialWoodBiomass, float initialLeafBiomass)
        {
            this.cohortData.Add(new CohortData(age, initialWoodBiomass, initialLeafBiomass));
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Gets the age of a cohort at a specific index.
        /// </summary>
        /// <exception cref="System.IndexOutOfRangeException">
        /// </exception>
        public int GetAge(int index)
        {
            return cohortData[index].Age;
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Combines all young cohorts into a single cohort whose age is the
        /// succession timestep - 1 and whose biomass is the sum of all the
        /// biomasses of the young cohorts.
        /// </summary>
        /// <remarks>
        /// The age of the combined cohort is set to the succession timestep -
        /// 1 so that when the combined cohort undergoes annual growth, its
        /// age will end up at the succession timestep.
        /// <p>
        /// For this method, young cohorts are those whose age is less than or
        /// equal to the succession timestep.  We include the cohort whose age
        /// is equal to the timestep because such a cohort is generated when
        /// reproduction occurs during a succession timestep.
        /// </remarks>
        public void CombineYoungCohorts()
        {
            //  Work from the end of cohort data since the array is in old-to-
            //  young order.
            int youngCount = 0;
            float totalWoodBiomass = 0;
            float totalLeafBiomass = 0;

            for (int i = cohortData.Count - 1; i >= 0; i--) {
                CohortData data = cohortData[i];
                if (data.Age <= Cohorts.SuccessionTimeStep) {
                    youngCount++;
                    totalWoodBiomass += data.WoodBiomass;
                    totalLeafBiomass += data.LeafBiomass;
                }
                else
                    break;
            }

            if (youngCount > 0) {
                cohortData.RemoveRange(cohortData.Count - youngCount, youngCount);
                cohortData.Add(new CohortData((ushort) (Cohorts.SuccessionTimeStep - 1),
                                              totalWoodBiomass, totalLeafBiomass));
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Grows an individual cohort for a year, incrementing its age by 1
        /// and updating its biomass for annual growth and mortality.
        /// </summary>
        /// <param name="index">
        /// The index of the cohort to grow; it must be between 0 and Count - 1.
        /// </param>
        /// <param name="site">
        /// The site where the species' cohorts are located.
        /// </param>
        /// <returns>
        /// The index of the next younger cohort.  Note this may be the same
        /// as the index passed in if that cohort dies due to senescence.
        /// </returns>
        public int GrowCohort(int        index,
                              ActiveSite site,
                              bool annualTimestep)
        {
            Debug.Assert(0 <= index && index <= cohortData.Count);
            Debug.Assert(site != null);

            Cohort cohort = new Cohort(species, cohortData[index]);
            Debug.Assert(cohort.WoodBiomass <= cohort.WoodBiomass + cohort.LeafBiomass);
            Debug.Assert(cohort.LeafBiomass <= cohort.WoodBiomass + cohort.LeafBiomass);

            //  Check for senescence
            if (cohort.Age >= species.Longevity) {
                RemoveCohort(index, cohort, site, null);
                return index;
            }

            if(annualTimestep) 
                cohort.IncrementAge();

            float[] biomassChange = Cohorts.BiomassCalculator.ComputeChange(cohort, site);

            Debug.Assert(-(cohort.WoodBiomass + cohort.LeafBiomass) <= biomassChange[0] + biomassChange[1]);  // Cohort can't loss more biomass than it has


            cohort.ChangeWoodBiomass(biomassChange[0]);
            cohort.ChangeLeafBiomass(biomassChange[1]);

            if (cohort.WoodBiomass + cohort.LeafBiomass > 0) {
                cohortData[index] = cohort.Data;
                return index + 1;
            }
            else {
                RemoveCohort(index, cohort, site, null);
                return index;
            }
        }

        //---------------------------------------------------------------------

        private void RemoveCohort(int        index,
                                  ICohort    cohort,
                                  ActiveSite site,
                                  ExtensionType disturbanceType)
        {
            cohortData.RemoveAt(index);
            Cohort.Died(this, cohort, site, disturbanceType);
        }

        private void ReduceCohort(int index,
                                  ICohort cohort,
                                  ActiveSite site,
                                  ExtensionType disturbanceType, float reduction)
        {
            //cohortData.RemoveAt(index);
            Cohort.PartialMortality(this, cohort, site, disturbanceType, reduction);
        }
        //---------------------------------------------------------------------

        /// <summary>
        /// Updates the IsMaturePresent property.
        /// </summary>
        /// <remarks>
        /// Should be called after all the species' cohorts have grown.
        /// </remarks>
        public void UpdateMaturePresent()
        {
            isMaturePresent = false;
            for (int i = 0; i < cohortData.Count; i++) {
                if (cohortData[i].Age >= species.Maturity) {
                    isMaturePresent = true;
                    break;
                }
            }
        }

        //---------------------------------------------------------------------

        /// <summary>
        /// Computes how much a disturbance damages the cohorts by reducing
        /// their biomass.
        /// </summary>
        /// <returns>
        /// The total of all the cohorts' biomass reductions.
        /// </returns>
        public int DamageBySpecies(IDisturbance disturbance)
        {
            //  Go backwards through list of cohort data, so the removal of an
            //  item doesn't mess up the loop.
            isMaturePresent = false;
            int totalReduction = 0;
            for (int i = cohortData.Count - 1; i >= 0; i--) {

                Cohort cohort = new Cohort(species, cohortData[i]);
                float[] reduction = disturbance.ReduceOrKillMarkedCohort(cohort);
                //int reduction = disturbance.ReduceOrKillMarkedCohort(cohort);
                totalReduction += (int)(reduction[0] + reduction[1]);
                float fRed = (totalReduction / (float) cohort.Biomass);

                if (totalReduction > 0)
                {
                    if (totalReduction < cohort.Biomass)
                    {
                        ReduceCohort(i, cohort, disturbance.CurrentSite, disturbance.Type, fRed); //RMS 12/2016
                        if (reduction[1] < cohort.LeafBiomass)
                        {
                            cohort.ChangeLeafBiomass(-reduction[1]);
                            cohortData[i] = cohort.Data;
                        }
                        if (reduction[0] < cohort.WoodBiomass)
                        {
                            cohort.ChangeWoodBiomass(-reduction[0]);
                            cohortData[i] = cohort.Data;
                        }
                    }
                    else
                    {  // Assume that if all wood biomass lost, the cohort is dead
                        RemoveCohort(i, cohort, disturbance.CurrentSite,
                                     disturbance.Type);
                        cohort = null;
                    }
                }
                if (cohort != null)
                {
                    if(cohort.Age >= species.Maturity)
                        isMaturePresent = true;
                }
            }
            return totalReduction;
        }

        //---------------------------------------------------------------------

        private static AgeOnlyCohorts.SpeciesCohortBoolArray isSpeciesCohortDamaged;

        //---------------------------------------------------------------------

        static SpeciesCohorts()
        {
            isSpeciesCohortDamaged = new AgeOnlyCohorts.SpeciesCohortBoolArray();
        }

        //---------------------------------------------------------------------
        
        /// <summary>
        /// Removes the cohorts that are damaged by an age-only disturbance.
        /// </summary>
        /// <returns>
        /// The total biomass of all the cohorts damaged by the disturbance.
        /// </returns>
        public int MarkCohorts(AgeOnlyCohorts.ISpeciesCohortsDisturbance disturbance)
        {
            isSpeciesCohortDamaged.SetAllFalse(Count);
            disturbance.MarkCohortsForDeath(this, isSpeciesCohortDamaged);

            //  Go backwards through list of cohort data, so the removal of an
            //  item doesn't mess up the loop.
            isMaturePresent = false;
            int totalReduction = 0;

            for (int i = cohortData.Count - 1; i >= 0; i--) {
                if (isSpeciesCohortDamaged[i]) {
                    Cohort cohort = new Cohort(species, cohortData[i]);
                    totalReduction += (int) (cohort.WoodBiomass + cohort.LeafBiomass);

                    Landis.Library.BiomassCohorts.Cohort.KilledByAgeOnlyDisturbance(disturbance, cohort, disturbance.CurrentSite, disturbance.Type);
                    
                    RemoveCohort(i, cohort, disturbance.CurrentSite,
                                 disturbance.Type);
                    cohort = null;
                }
                else if (cohortData[i].Age >= species.Maturity)
                    isMaturePresent = true;
            }
            return totalReduction;
        }
        
        //--------------------------------------------------------------------- 
        public int MarkCohorts(Landis.Library.BiomassCohorts.IDisturbance disturbance)
        {
            //  Go backwards through list of cohort data, so the removal of an
            //  item doesn't mess up the loop.
            isMaturePresent = false;
            int totalReduction = 0;

            for (int i = cohortData.Count - 1; i >= 0; i--)
            {
                Cohort cohort = new Cohort(species, cohortData[i]);
                int reduction = disturbance.ReduceOrKillMarkedCohort(cohort);
                if (reduction > 0)
                {
                    totalReduction += reduction;
                    if (reduction < cohort.Biomass)
                    {

                        float fRed = (float)reduction / (float)cohort.Biomass;
                        ReduceCohort(i, cohort, disturbance.CurrentSite, disturbance.Type, fRed);  //RMS 12/2016
                        
                        float deltaWood = (-1) * fRed * (float)cohort.Data.WoodBiomass;
                        cohort.ChangeWoodBiomass(deltaWood);
                        float deltaLeaf = (-1) * fRed * (float)cohort.Data.LeafBiomass;
                        cohort.ChangeLeafBiomass(deltaLeaf);

                        if (cohortData[i].Age >= species.Maturity)
                            isMaturePresent = true;
                    }
                    else
                    {
                        RemoveCohort(i, cohort, disturbance.CurrentSite, disturbance.Type);
                        cohort = null;
                    }
                }
                else
                {
                    if (cohortData[i].Age >= species.Maturity)
                        isMaturePresent = true;
                }
            }
            return totalReduction;
        }
        //---------------------------------------------------------------------
        public float MarkCohorts(Landis.Library.BiomassCohorts.ISpeciesCohortsDisturbance disturbance)
        {
            throw new System.Exception("Cannot implement MarkCohorts");
        }
        //---------------------------------------------------------------------

        IEnumerator<ICohort> IEnumerable<ICohort>.GetEnumerator()
        {
            foreach (CohortData data in cohortData)
                yield return new Cohort(species, data);
        }

        //---------------------------------------------------------------------

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<ICohort>)this).GetEnumerator();
        }

        //---------------------------------------------------------------------
        
        IEnumerator<Landis.Library.AgeOnlyCohorts.ICohort> IEnumerable<Landis.Library.AgeOnlyCohorts.ICohort>.GetEnumerator()
        {
            foreach (CohortData data in cohortData)
                yield return new Landis.Library.AgeOnlyCohorts.Cohort(species, data.Age);
        }
        
        IEnumerator<Landis.Library.BiomassCohorts.ICohort> IEnumerable<Landis.Library.BiomassCohorts.ICohort>.GetEnumerator()
        {
            foreach (CohortData data in cohortData)
                yield return new Landis.Library.BiomassCohorts.Cohort(species, data.Age, (int)data.WoodBiomass);
        }

    }
}
