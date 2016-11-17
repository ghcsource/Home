namespace Walkmap.DAL.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Trail")]
    public partial class Trail
    {
        public long ID { get; set; }

        public long? DeviceId { get; set; }

        [Column(TypeName = "real")]
        public double? Latitude { get; set; }

        [Column(TypeName = "real")]
        public double? Longitude { get; set; }

        [Column(TypeName = "real")]
        public double? LatitudeForMap { get; set; }

        [Column(TypeName = "real")]
        public double? LongitudeForMap { get; set; }

        [StringLength(2147483647)]
        public string PositionSource { get; set; }

        [StringLength(2147483647)]
        public string CreateTime { get; set; }

        public virtual Device Device { get; set; }
    }
}
