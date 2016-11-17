namespace Walkmap.DAL.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Device")]
    public partial class Device
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Device()
        {
            Trail = new HashSet<Trail>();
        }

        public long ID { get; set; }

        [Required]
        [StringLength(2147483647)]
        public string DeviceUniqueId { get; set; }

        public long? UserID { get; set; }

        [StringLength(2147483647)]
        public string DeviceName { get; set; }

        public virtual User User { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Trail> Trail { get; set; }
    }
}
